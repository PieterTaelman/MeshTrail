using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Google.Protobuf;
using Meshtastic.Protobufs;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace Meshtrail.Mesh.Mqtt;

/// <summary>Where and how to log in to the Meshtrail broker with the service account.</summary>
public sealed class MeshtasticMqttClientOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1883;

    public string UserName { get; set; } = "meshtrail-api";

    public string Password { get; set; } = string.Empty;

    /// <summary>Topic filter to receive; "#" = every gateway uplink (only the service login gets them).</summary>
    public string Subscription { get; set; } = "#";
}

/// <summary>
/// The Meshtrail side of the broker: logs in with the service account, receives every gateway uplink and publishes
/// downlink packets. Used by the API's gateway transport. Reconnecting is the caller's job.
/// </summary>
public sealed class MeshtasticMqttClient(MeshtasticMqttClientOptions options, TimeProvider timeProvider) : IAsyncDisposable
{
    private readonly IMqttClient _client = new MqttClientFactory().CreateMqttClient();
    private Channel<MqttUplink> _uplinks = System.Threading.Channels.Channel.CreateUnbounded<MqttUplink>();

    public bool IsConnected => _client.IsConnected;

    public string Description => $"mqtt://{options.Host}:{options.Port}";

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _uplinks = System.Threading.Channels.Channel.CreateUnbounded<MqttUplink>();
        _client.ApplicationMessageReceivedAsync -= OnMessageAsync;
        _client.ApplicationMessageReceivedAsync += OnMessageAsync;
        _client.DisconnectedAsync -= OnDisconnectedAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;

        var connectOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(options.Host, options.Port)
            .WithClientId($"meshtrail-{Guid.NewGuid():N}")
            .WithCredentials(options.UserName, options.Password)
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithCleanSession()
            .Build();

        var result = await _client.ConnectAsync(connectOptions, cancellationToken);
        if (result.ResultCode != MqttClientConnectResultCode.Success)
        {
            throw new InvalidOperationException($"The MQTT broker refused the login: {result.ResultCode}.");
        }

        // NoLocal: do not hand our own downlinks back to us.
        var subscribe = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(filter => filter.WithTopic(options.Subscription).WithNoLocal())
            .Build();
        await _client.SubscribeAsync(subscribe, cancellationToken);
    }

    /// <summary>
    /// Every message the broker routes to us, in arrival order: gateway uplinks (UserName = the gateway login), the
    /// broker's <see cref="MeshtrailMqtt.ConnectionsTopic"/> snapshots and other service logins' downlinks (no UserName).
    /// Ends when the connection is lost.
    /// </summary>
    public async IAsyncEnumerable<MqttUplink> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var uplink in _uplinks.Reader.ReadAllAsync(cancellationToken))
        {
            yield return uplink;
        }
    }

    /// <summary>
    /// Sends a packet down: gateways with downlink enabled on <paramref name="channel"/> transmit it. With
    /// <paramref name="targetGateway"/> (a gateway login) the Meshtrail broker delivers it to that gateway only.
    /// </summary>
    public Task PublishPacketAsync(
        string root, string channel, string senderId, MeshPacket packet, string? targetGateway, CancellationToken cancellationToken)
    {
        var envelope = new ServiceEnvelope { Packet = packet, ChannelId = channel, GatewayId = senderId };
        return PublishAsync(MeshtasticTopic.ForEnvelope(root, channel, senderId), envelope.ToByteArray(), targetGateway, cancellationToken);
    }

    public async Task PublishAsync(string topic, byte[] payload, string? targetGateway, CancellationToken cancellationToken)
    {
        var builder = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce);
        if (targetGateway is not null)
        {
            builder.WithUserProperty(MeshtrailMqtt.TargetProperty, Encoding.UTF8.GetBytes(targetGateway).AsMemory());
        }

        await _client.PublishAsync(builder.Build(), cancellationToken);
    }

    public async Task DisconnectAsync()
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync();
        }

        _uplinks.Writer.TryComplete();
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _client.Dispose();
    }

    private Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        // The broker stamped the gateway's login on the message; downlinks of other service logins have none.
        var message = args.ApplicationMessage;
        var gateway = MeshtrailMqtt.GetProperty(message, MeshtrailMqtt.GatewayProperty);
        _uplinks.Writer.TryWrite(MqttUplink.From(timeProvider.GetUtcNow(), null, gateway, message.Topic, message.Payload.ToArray()));
        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        _uplinks.Writer.TryComplete(args.Exception);
        return Task.CompletedTask;
    }
}
