using Mediator;
using Meshtastic.Protobufs;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.FailTimedOutMessages;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.MarkMessageSent;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordGatewayStatus;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RequeuePendingMessages;
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
/// packet), send queued requests within the rate limit, fail messages that never got a delivery report, and
/// reconnect with growing pauses when the link drops.
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
    /// <summary>How often we look for sent messages whose delivery report is overdue.</summary>
    private static readonly TimeSpan AckSweepInterval = TimeSpan.FromSeconds(15);

    private readonly string _mode = radioOptions.Value.Mode.ToString();
    private readonly TimeSpan _ackTimeout = outboundOptions.Value.AckTimeout;
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

                // Messages stored while we were offline (or before a restart) go out now.
                await SendInScopeAsync(new RequeuePendingMessagesCommand(), stoppingToken);

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
        var sweeping = AckSweepLoopAsync(stopSending.Token);

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
            await sweeping;
        }
    }

    private async Task HandleAsync(PacketTranslator translator, FromRadio message, CancellationToken cancellationToken)
    {
        var events = translator.Translate(message, timeProvider.GetUtcNow());
        gateway.GatewayNodeNum = translator.GatewayNodeNum ?? gateway.GatewayNodeNum;
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
                try
                {
                    await SendAsync(request, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // A text message stays Queued in the database and is tried again after the next connect.
                    LogSendFailed(exception, request.GetType().Name);
                }
                finally
                {
                    gateway.Complete(request);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The connection ended; queued requests wait for the next one.
        }
    }

    private async Task SendAsync(MeshOutboundRequest request, CancellationToken cancellationToken)
    {
        if (request is AddContactRequest contact)
        {
            // Admin message to our own gateway: it never goes on the air, so no duty-cycle wait.
            if (gateway.GatewayNodeNum is not { } gatewayNodeNum)
            {
                LogContactSkipped(contact.NodeNum);
                return;
            }

            await radio.SendAsync(
                MeshPackets.AddContact(gatewayNodeNum, contact.NodeNum, contact.LongName, contact.ShortName, contact.PublicKey, contact.PacketId),
                cancellationToken);
            LogSent(request.GetType().Name, request.NodeNum, request.PacketId);
            return;
        }

        await _rateLimiter.WaitTurnAsync(cancellationToken);
        await radio.SendAsync(ToRadio(request), cancellationToken);
        LogSent(request.GetType().Name, request.NodeNum, request.PacketId);

        if (request is TextMessageRequest text)
        {
            await SendInScopeAsync(new MarkMessageSentCommand(text.MessageId), cancellationToken);
        }
    }

    private static ToRadio ToRadio(MeshOutboundRequest request) => request switch
    {
        PositionRequest position => MeshPackets.PositionRequest(position.NodeNum, position.PacketId),
        TracerouteRequest traceroute => MeshPackets.Traceroute(traceroute.NodeNum, traceroute.PacketId),
        TextMessageRequest text => MeshPackets.Text(text.NodeNum, (uint)text.Channel, text.Text, text.PacketId),
        _ => throw new NotSupportedException($"Unknown outbound request {request.GetType().Name}."),
    };

    private async Task AckSweepLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(AckSweepInterval, timeProvider, cancellationToken);
                await SendInScopeAsync(new FailTimedOutMessagesCommand(timeProvider.GetUtcNow() - _ackTimeout), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Connection ended; the next connection starts a new sweep.
        }
    }

    /// <summary>Sends a command in its own DI scope and only logs failures (the worker must keep running).</summary>
    private async Task SendInScopeAsync<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : notnull, IMessage
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send((object)command, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPacketFailed(exception, typeof(TCommand).Name);
        }
    }

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Contact for node {NodeNum} not sent: the gateway's own node number is not known yet")]
    private partial void LogContactSkipped(uint nodeNum);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save gateway status {Status}")]
    private partial void LogStatusNotSaved(Exception exception, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Disconnecting from the mesh gateway failed")]
    private partial void LogDisconnectFailed(Exception exception);
}
