using Meshtastic.Protobufs;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Mesh.Outbound;
using Meshtrail.Mesh.Radio;
using Microsoft.Extensions.Logging;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>
/// A local base station over the Meshtastic TCP API (one node on the LAN). The node accepts only one TCP client: when
/// the phone app or Home Assistant connects, we are kicked off and reconnect with growing pauses.
/// </summary>
public sealed partial class TcpGatewayTransport(IMeshRadio radio, TimeProvider timeProvider, ILogger<TcpGatewayTransport> logger)
    : IGatewayTransport
{
    private uint? _nodeNum;

    public GatewayTransport Kind => GatewayTransport.Tcp;

    public string? Broker => null;

    public string Description => radio.Description;

    public bool IsConnected => radio.State == MeshRadioState.Connected;

    public string? LastError { get; private set; } = "Not started yet.";

    public async Task RunAsync(GatewayInbox inbox, CancellationToken stoppingToken)
    {
        var failedAttempts = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            string error;
            try
            {
                await radio.ConnectAsync(stoppingToken);
                failedAttempts = 0;
                LastError = null;

                await foreach (var message in radio.ReadAllAsync(stoppingToken))
                {
                    if (message.PayloadVariantCase == FromRadio.PayloadVariantOneofCase.MyInfo && message.MyInfo.MyNodeNum != 0)
                    {
                        // The node says who it is: from now on its packets have a gateway.
                        _nodeNum = message.MyInfo.MyNodeNum;
                        inbox.Post(new GatewayConnectionInput(Source(), true, null, timeProvider.GetUtcNow()));
                    }

                    inbox.Post(new GatewayPacketInput(Source(), null, null, message, timeProvider.GetUtcNow()));
                }

                error = "The gateway closed the connection.";
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                LogConnectionFailed(exception, radio.Description);
            }

            LastError = error;
            await DisconnectQuietlyAsync();
            if (_nodeNum is not null)
            {
                inbox.Post(new GatewayConnectionInput(Source(), false, error, timeProvider.GetUtcNow()));
            }

            var delay = ReconnectBackoff.Delay(failedAttempts++, Random.Shared.NextDouble());
            LogRetrying(delay.TotalSeconds, error);
            try
            {
                await Task.Delay(delay, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await DisconnectQuietlyAsync();
        if (_nodeNum is not null)
        {
            inbox.Post(new GatewayConnectionInput(Source(), false, "Server is shutting down.", timeProvider.GetUtcNow()));
        }
    }

    public Task SendAsync(GatewayRoute via, MeshPacket packet, CancellationToken cancellationToken) =>
        radio.SendAsync(MeshPackets.ToRadio(packet), cancellationToken);

    private GatewaySource Source() => new(GatewayTransport.Tcp, _nodeNum, null, null);

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not connect to the TCP gateway {Target}")]
    private partial void LogConnectionFailed(Exception exception, string target);

    [LoggerMessage(Level = LogLevel.Information, Message = "TCP gateway offline ({Reason}); retrying in {DelaySeconds:0.0} s")]
    private partial void LogRetrying(double delaySeconds, string? reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Disconnecting from the TCP gateway failed")]
    private partial void LogDisconnectFailed(Exception exception);
}
