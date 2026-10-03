using Mediator;
using Meshtastic.Protobufs;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordGatewayStatus;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Mesh.Events;
using Meshtrail.Mesh.Outbound;
using Meshtrail.Mesh.Radio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>
/// Owns the connection to the gateway node: connect, read every packet, turn it into commands (one DI scope per
/// packet), send queued requests within the rate limit, and reconnect with growing pauses when the link drops.
/// It never throws: a radio problem may not take the API down.
/// </summary>
public sealed partial class MeshGatewayWorker(
    IMeshRadio radio,
    MeshGatewayService gateway,
    IServiceScopeFactory scopeFactory,
    IOptions<MeshRadioOptions> radioOptions,
    IOptions<MeshOutboundOptions> outboundOptions,
    TimeProvider timeProvider,
    ILogger<MeshGatewayWorker> logger) : BackgroundService
{
    private readonly string _mode = radioOptions.Value.Mode.ToString();
    private readonly OutboundRateLimiter _rateLimiter = new(outboundOptions.Value.MinInterval, timeProvider);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failedAttempts = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var reconnectToken = gateway.NextReconnectToken();
            using var connection = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, reconnectToken);
            string? error;

            try
            {
                await RecordStatusAsync(GatewayStatus.Connecting, null, stoppingToken);
                await radio.ConnectAsync(connection.Token);
                failedAttempts = 0;
                await RecordStatusAsync(GatewayStatus.Online, null, stoppingToken);

                error = await RunConnectionAsync(connection.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException) when (reconnectToken.IsCancellationRequested)
            {
                error = "Reconnect requested.";
            }
            catch (Exception exception)
            {
                error = exception.Message;
                LogConnectionFailed(exception, radio.Description);
            }
            finally
            {
                await DisconnectQuietlyAsync();
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await RecordStatusAsync(GatewayStatus.Offline, error, stoppingToken);

            // A requested reconnect goes again immediately; a failure waits a bit longer each time.
            if (!reconnectToken.IsCancellationRequested)
            {
                var delay = ReconnectBackoff.Delay(failedAttempts++, Random.Shared.NextDouble());
                LogRetrying(delay.TotalSeconds, error);
                await WaitAsync(delay, gateway.NextReconnectToken(), stoppingToken);
            }
        }

        await RecordStatusAsync(GatewayStatus.Offline, "Server is shutting down.", CancellationToken.None);
    }

    /// <summary>Reads until the connection ends. Returns why it ended.</summary>
    private async Task<string> RunConnectionAsync(CancellationToken cancellationToken)
    {
        var translator = new PacketTranslator();
        using var stopSending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var sending = SendLoopAsync(stopSending.Token);

        try
        {
            await foreach (var message in radio.ReadAllAsync(cancellationToken))
            {
                await HandleAsync(translator, message, cancellationToken);
            }

            return "The gateway closed the connection.";
        }
        finally
        {
            await stopSending.CancelAsync();
            await sending;
        }
    }

    private async Task HandleAsync(PacketTranslator translator, FromRadio message, CancellationToken cancellationToken)
    {
        var events = translator.Translate(message, timeProvider.GetUtcNow());
        if (events.Count == 0)
        {
            return;
        }

        // One scope per packet, like one HTTP request: its own DbContext, so one bad packet cannot poison the next.
        await using var scope = scopeFactory.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        foreach (var meshEvent in events)
        {
            if (MeshEventCommands.ToCommand(meshEvent) is not { } command)
            {
                continue;
            }

            try
            {
                await sender.Send(command, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Log and carry on: losing one packet is better than losing the connection.
                LogPacketFailed(exception, command.GetType().Name);
            }
        }
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var request in gateway.Outbound.ReadAllAsync(cancellationToken))
            {
                await _rateLimiter.WaitTurnAsync(cancellationToken);
                try
                {
                    await radio.SendAsync(ToRadio(request), cancellationToken);
                    LogSent(request.GetType().Name, request.NodeNum, request.PacketId);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    LogSendFailed(exception, request.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The connection ended; queued requests wait for the next one.
        }
    }

    private static ToRadio ToRadio(MeshOutboundRequest request) => request switch
    {
        PositionRequest position => MeshPackets.PositionRequest(position.NodeNum, position.PacketId),
        TracerouteRequest traceroute => MeshPackets.Traceroute(traceroute.NodeNum, traceroute.PacketId),
        _ => throw new NotSupportedException($"Unknown outbound request {request.GetType().Name}."),
    };

    private async Task RecordStatusAsync(GatewayStatus status, string? error, CancellationToken cancellationToken)
    {
        gateway.SetStatus(status, error);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new RecordGatewayStatusCommand(status, _mode, error), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The database may be down too; the in-memory status (health check) is still correct.
            LogStatusNotSaved(exception, status.ToString());
        }
    }

    private async Task DisconnectQuietlyAsync()
    {
        try
        {
            await radio.DisconnectAsync();
        }
        catch (Exception exception)
        {
            LogDisconnectFailed(exception);
        }
    }

    private async Task WaitAsync(TimeSpan delay, CancellationToken reconnectToken, CancellationToken stoppingToken)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(reconnectToken, stoppingToken);
        try
        {
            await Task.Delay(delay, timeProvider, wait.Token);
        }
        catch (OperationCanceledException)
        {
            // Woken early by a reconnect request or shutdown.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not connect to the mesh gateway {Target}")]
    private partial void LogConnectionFailed(Exception exception, string target);

    [LoggerMessage(Level = LogLevel.Information, Message = "Mesh gateway offline ({Reason}); retrying in {DelaySeconds:0.0} s")]
    private partial void LogRetrying(double delaySeconds, string? reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Handling {Command} from the mesh failed")]
    private partial void LogPacketFailed(Exception exception, string command);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent {Request} to node {NodeNum} (packet {PacketId})")]
    private partial void LogSent(string request, uint nodeNum, uint packetId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sending {Request} to the mesh failed")]
    private partial void LogSendFailed(Exception exception, string request);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save gateway status {Status}")]
    private partial void LogStatusNotSaved(Exception exception, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Disconnecting from the mesh gateway failed")]
    private partial void LogDisconnectFailed(Exception exception);
}
