using System.Buffers;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace Meshtrail.Mesh.Mqtt;

/// <summary>Something a gateway published to us. Envelope is set for <see cref="MeshtasticTopicKind.Envelope"/> topics.</summary>
public sealed record MqttUplink(
    DateTimeOffset ReceivedAt,
    string ClientId,
    string? UserName,
    string RawTopic,
    MeshtasticTopic? Topic,
    ServiceEnvelope? Envelope,
    byte[] Payload)
{
    /// <summary>Payload as text, for JSON and status topics.</summary>
    public string PayloadText => Encoding.UTF8.GetString(Payload);
}

/// <summary>Settings of the embedded broker.</summary>
public sealed class MeshtasticMqttBrokerOptions
{
    /// <summary>Plain MQTT port. Only for the LAN / development; production uses TLS.</summary>
    public int Port { get; set; } = 1883;

    /// <summary>Address to listen on; Any = every network card, so nodes on the WiFi can reach it.</summary>
    public IPAddress BindAddress { get; set; } = IPAddress.Any;
}

/// <summary>
/// An MQTT broker inside our own process that Meshtastic gateways connect to (their MQTT module). It hands every
/// published message to us and lets us publish downlink packets. We only route our own traffic: gateways never
/// receive each other's messages unless we publish them.
/// </summary>
public sealed partial class MeshtasticMqttBroker : IAsyncDisposable
{
    private readonly MqttServer _server;
    private readonly Channel<MqttUplink> _uplinks = System.Threading.Channels.Channel.CreateUnbounded<MqttUplink>();
    private readonly Func<string, string?, string?, bool> _authenticate;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <param name="authenticate">(clientId, userName, password) → allowed? Never log the password.</param>
    public MeshtasticMqttBroker(
        MeshtasticMqttBrokerOptions options,
        Func<string, string?, string?, bool> authenticate,
        TimeProvider timeProvider,
        ILogger logger)
    {
        _authenticate = authenticate;
        _timeProvider = timeProvider;
        _logger = logger;

        var serverOptions = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(options.Port)
            .WithDefaultEndpointBoundIPAddress(options.BindAddress)
            .Build();
        _server = new MqttServerFactory().CreateMqttServer(serverOptions);
        _server.ValidatingConnectionAsync += ValidateAsync;
        _server.InterceptingPublishAsync += InterceptAsync;
        _server.ClientConnectedAsync += args =>
        {
            LogConnected(_logger, args.ClientId, args.UserName, args.RemoteEndPoint?.ToString());
            return Task.CompletedTask;
        };
        _server.ClientDisconnectedAsync += args =>
        {
            LogDisconnected(_logger, args.ClientId, args.ReasonCode.ToString() ?? "unknown");
            return Task.CompletedTask;
        };
    }

    public Task StartAsync() => _server.StartAsync();

    public Task StopAsync() => _server.StopAsync();

    /// <summary>Everything gateways publish, in arrival order.</summary>
    public async IAsyncEnumerable<MqttUplink> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var uplink in _uplinks.Reader.ReadAllAsync(cancellationToken))
        {
            yield return uplink;
        }
    }

    /// <summary>Publishes a mesh packet as a ServiceEnvelope; gateways with downlink on that channel transmit it.</summary>
    public Task PublishPacketAsync(string root, string channel, string senderId, MeshPacket packet, CancellationToken cancellationToken)
    {
        var envelope = new ServiceEnvelope { Packet = packet, ChannelId = channel, GatewayId = senderId };
        return PublishAsync(MeshtasticTopic.ForEnvelope(root, channel, senderId), envelope.ToByteArray(), cancellationToken);
    }

    public async Task PublishAsync(string topic, byte[] payload, CancellationToken cancellationToken)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build();
        await _server.InjectApplicationMessage(new InjectedMqttApplicationMessage(message) { SenderClientId = "meshtrail" }, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.StopAsync();
        _server.Dispose();
        _uplinks.Writer.TryComplete();
    }

    private Task ValidateAsync(ValidatingConnectionEventArgs args)
    {
        args.ReasonCode = _authenticate(args.ClientId, args.UserName, args.Password)
            ? MqttConnectReasonCode.Success
            : MqttConnectReasonCode.BadUserNameOrPassword;
        if (args.ReasonCode != MqttConnectReasonCode.Success)
        {
            LogRejected(_logger, args.ClientId, args.UserName);
        }

        return Task.CompletedTask;
    }

    private Task InterceptAsync(InterceptingPublishEventArgs args)
    {
        // Our own downlink messages also pass through here; they are not uplinks.
        if (args.ClientId == "meshtrail" || string.IsNullOrEmpty(args.ClientId))
        {
            return Task.CompletedTask;
        }

        var message = args.ApplicationMessage;
        var payload = message.Payload.ToArray();
        MeshtasticTopic.TryParse(message.Topic, out var topic);
        var parsedTopic = topic.Root.Length > 0 ? topic : null;

        ServiceEnvelope? envelope = null;
        if (parsedTopic?.Kind == MeshtasticTopicKind.Envelope)
        {
            try
            {
                envelope = ServiceEnvelope.Parser.ParseFrom(payload);
            }
            catch (InvalidProtocolBufferException)
            {
                LogBadEnvelope(_logger, message.Topic, payload.Length);
            }
        }

        _uplinks.Writer.TryWrite(new MqttUplink(_timeProvider.GetUtcNow(), args.ClientId, args.UserName, message.Topic, parsedTopic, envelope, payload));
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "MQTT client {ClientId} connected (user {UserName}, from {RemoteEndPoint})")]
    private static partial void LogConnected(ILogger logger, string clientId, string? userName, string? remoteEndPoint);

    [LoggerMessage(Level = LogLevel.Information, Message = "MQTT client {ClientId} disconnected ({Reason})")]
    private static partial void LogDisconnected(ILogger logger, string clientId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MQTT client {ClientId} rejected (user {UserName})")]
    private static partial void LogRejected(ILogger logger, string clientId, string? userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unreadable ServiceEnvelope on {Topic} ({Length} bytes)")]
    private static partial void LogBadEnvelope(ILogger logger, string topic, int length);
}
