using Meshtastic.Protobufs;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Mesh;
using Meshtrail.Mesh.Mqtt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>
/// The API's link to one Meshtrail MQTT broker: logs in with the service account, turns gateway uplinks into inputs
/// and sends downlinks to one gateway (the broker delivers a targeted downlink to that gateway only).
/// </summary>
public sealed partial class MqttGatewayTransport(
    IOptions<MeshMqttOptions> mqttOptions,
    IOptions<MeshOutboundOptions> outboundOptions,
    TimeProvider timeProvider,
    ILogger<MqttGatewayTransport> logger) : IGatewayTransport, IAsyncDisposable
{
    private readonly MeshMqttOptions _options = mqttOptions.Value;
    private readonly uint _virtualNodeNum = outboundOptions.Value.VirtualNodeNum;
    private int _disposed;
    private readonly MeshtasticMqttClient _client = new(
        new MeshtasticMqttClientOptions
        {
            Host = mqttOptions.Value.Host,
            Port = mqttOptions.Value.Port,
            UserName = mqttOptions.Value.UserName,
            Password = mqttOptions.Value.Password,
        },
        timeProvider);

    public GatewayTransport Kind => GatewayTransport.Mqtt;

    public string? Broker => _options.Broker;

    public string Description => $"mqtt://{_options.Host}:{_options.Port} (broker {_options.Broker})";

    public bool IsConnected => _client.IsConnected;

    public string? LastError { get; private set; } = "Not started yet.";

    public async Task RunAsync(GatewayInbox inbox, CancellationToken stoppingToken)
    {
        var failedAttempts = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _client.ConnectAsync(stoppingToken);
                failedAttempts = 0;
                LastError = null;
                LogConnected(Description);

                await foreach (var uplink in _client.ReadAllAsync(stoppingToken))
                {
                    if (ToInput(uplink) is { } input)
                    {
                        inbox.Post(input);
                    }
                }

                LastError = "The broker closed the connection.";
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                LogConnectionFailed(exception, Description);
            }

            await DisconnectQuietlyAsync();
            var delay = ReconnectBackoff.Delay(failedAttempts++, Random.Shared.NextDouble());
            LogRetrying(delay.TotalSeconds, LastError);
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
    }

    public async Task SendAsync(GatewayRoute via, MeshPacket packet, CancellationToken cancellationToken)
    {
        if (!_client.IsConnected)
        {
            throw new InvalidOperationException($"Not connected to the MQTT broker {_options.Broker}.");
        }

        // The gateway transmits the packet on the channel it uplinks, as if our virtual node sent it.
        await _client.PublishPacketAsync(
            via.MqttRoot ?? _options.Root,
            via.Channel ?? _options.DefaultChannel,
            NodeIds.Format(_virtualNodeNum),
            packet,
            via.MqttUserName,
            cancellationToken);
    }

    /// <summary>Safe to call twice (DI disposes it once per registration).</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await _client.DisposeAsync();
        }
    }

    /// <summary>Turns one MQTT message into an input, or null when it is nothing for us.</summary>
    internal GatewayInput? ToInput(MqttUplink uplink)
    {
        if (uplink.RawTopic == MeshtrailMqtt.ConnectionsTopic)
        {
            return MeshtrailMqtt.DeserializeConnections(uplink.Payload) is { } connections
                ? new BrokerConnectionsInput(_options.Broker, connections.UserNames ?? [], uplink.ReceivedAt)
                : null;
        }

        // Only envelopes the broker stamped with a gateway login (not our own or another service's downlinks).
        if (uplink.UserName is not { Length: > 0 } login || uplink.Envelope?.Packet is not { } packet || uplink.Topic is not { } topic)
        {
            return null;
        }

        var gatewayId = string.IsNullOrEmpty(uplink.Envelope.GatewayId) ? topic.GatewayId : uplink.Envelope.GatewayId;
        if (!NodeIds.TryParse(gatewayId, out var gatewayNodeNum))
        {
            return null;
        }

        // Packets that came from MQTT (another broker) or from ourselves are not news.
        if (packet.ViaMqtt || packet.From == _virtualNodeNum || packet.From == 0)
        {
            return null;
        }

        var channel = string.IsNullOrEmpty(topic.Channel) ? uplink.Envelope.ChannelId : topic.Channel;
        return new GatewayPacketInput(
            new GatewaySource(GatewayTransport.Mqtt, gatewayNodeNum, login, _options.Broker),
            channel,
            topic.Root,
            new FromRadio { Packet = packet },
            uplink.ReceivedAt);
    }

    private async Task DisconnectQuietlyAsync()
    {
        try
        {
            await _client.DisconnectAsync();
        }
        catch (Exception exception)
        {
            LogDisconnectFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to the MQTT broker {Target}")]
    private partial void LogConnected(string target);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not connect to the MQTT broker {Target}")]
    private partial void LogConnectionFailed(Exception exception, string target);

    [LoggerMessage(Level = LogLevel.Information, Message = "MQTT broker offline ({Reason}); retrying in {DelaySeconds:0.0} s")]
    private partial void LogRetrying(double delaySeconds, string? reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Disconnecting from the MQTT broker failed")]
    private partial void LogDisconnectFailed(Exception exception);
}
