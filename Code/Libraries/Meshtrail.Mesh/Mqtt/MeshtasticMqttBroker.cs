using System.Buffers;
using System.Collections.Concurrent;
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

/// <summary>Something a gateway published. Envelope is set for <see cref="MeshtasticTopicKind.Envelope"/> topics.</summary>
public sealed record MqttUplink(
    DateTimeOffset ReceivedAt,
    string? ClientId,
    string? UserName,
    string RawTopic,
    MeshtasticTopic? Topic,
    ServiceEnvelope? Envelope,
    byte[] Payload)
{
    /// <summary>Payload as text, for JSON and status topics.</summary>
    public string PayloadText => Encoding.UTF8.GetString(Payload);

    /// <summary>Parses a received MQTT message into an uplink (topic + envelope when it is one).</summary>
    public static MqttUplink From(DateTimeOffset receivedAt, string? clientId, string? userName, string topic, byte[] payload)
    {
        var parsedTopic = MeshtasticTopic.TryParse(topic, out var parsed) ? parsed : null;
        ServiceEnvelope? envelope = null;
        if (parsedTopic?.Kind == MeshtasticTopicKind.Envelope)
        {
            try
            {
                envelope = ServiceEnvelope.Parser.ParseFrom(payload);
            }
            catch (InvalidProtocolBufferException)
            {
                // Not a valid envelope: keep the raw payload, the caller decides what to do.
            }
        }

        return new MqttUplink(receivedAt, clientId, userName, topic, parsedTopic, envelope, payload);
    }
}

/// <summary>Settings of the Meshtrail MQTT broker (section MqttBroker).</summary>
public sealed class MeshtasticMqttBrokerOptions
{
    public const string SectionName = "MqttBroker";

    /// <summary>Plain MQTT port. TLS (8883) comes with production hosting.</summary>
    public int Port { get; set; } = 1883;

    /// <summary>Address to listen on; 0.0.0.0 = every network card, so nodes on the network can reach it.</summary>
    public string BindAddress { get; set; } = "0.0.0.0";

    /// <summary>Login of the Meshtrail API. Only this client receives gateway traffic and may send to gateways.</summary>
    public string ServiceUserName { get; set; } = "meshtrail-api";

    /// <summary>Password of the service login. A secret: user secrets / environment, never in git (except dev defaults).</summary>
    public string ServicePassword { get; set; } = string.Empty;

    /// <summary>Development only: accept any gateway login (until gateways get their own credentials).</summary>
    public bool AllowAnyGateway { get; set; }

    /// <summary>Messages per second one gateway may publish; more is dropped (protects the platform from broken nodes).</summary>
    public int MaxMessagesPerSecondPerGateway { get; set; } = 20;

    /// <summary>Write a one-line summary of every uplink to the log (development / troubleshooting).</summary>
    public bool LogUplinks { get; set; }
}

/// <summary>
/// The broker Meshtastic gateways connect to (their MQTT module). It is a strict router, not a free-for-all broker:
/// <list type="bullet">
/// <item>gateways publish uplinks; only the Meshtrail API (service login) receives them;</item>
/// <item>only the API (or this process) may send to gateways — a gateway never receives another gateway's traffic,
/// otherwise every gateway would re-transmit everything it got from MQTT over the air;</item>
/// <item>each gateway is rate-limited.</item>
/// </list>
/// </summary>
public sealed partial class MeshtasticMqttBroker : IAsyncDisposable
{
    private const string InternalClientId = "meshtrail-broker";
    private const string ServiceRole = "service";

    private readonly MeshtasticMqttBrokerOptions _options;
    private readonly MqttServer _server;
    private readonly Channel<MqttUplink> _uplinks = System.Threading.Channels.Channel.CreateBounded<MqttUplink>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly ConcurrentDictionary<string, string> _roles = new();
    private readonly ConcurrentDictionary<string, RateWindow> _rates = new();
    private readonly Func<string, string?, string?, bool> _authenticateGateway;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <param name="authenticateGateway">(clientId, userName, password) → is this a known gateway? Never log the password.</param>
    public MeshtasticMqttBroker(
        MeshtasticMqttBrokerOptions options,
        Func<string, string?, string?, bool> authenticateGateway,
        TimeProvider timeProvider,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(options.ServicePassword))
        {
            throw new InvalidOperationException($"Set {MeshtasticMqttBrokerOptions.SectionName}:ServicePassword (user secrets or environment).");
        }

        _options = options;
        _authenticateGateway = authenticateGateway;
        _timeProvider = timeProvider;
        _logger = logger;

        var serverOptions = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(options.Port)
            .WithDefaultEndpointBoundIPAddress(IPAddress.Parse(options.BindAddress))
            .Build();
        _server = new MqttServerFactory().CreateMqttServer(serverOptions);
        _server.ValidatingConnectionAsync += ValidateAsync;
        _server.InterceptingPublishAsync += InterceptPublishAsync;
        _server.InterceptingClientEnqueueAsync += InterceptEnqueueAsync;
        _server.ClientConnectedAsync += args =>
        {
            LogConnected(_logger, args.ClientId, RoleOf(args.ClientId), args.UserName, args.RemoteEndPoint?.ToString());
            return Task.CompletedTask;
        };
        _server.ClientDisconnectedAsync += args =>
        {
            _roles.TryRemove(args.ClientId, out _);
            _rates.TryRemove(args.ClientId, out _);
            LogDisconnected(_logger, args.ClientId, args.ReasonCode.ToString() ?? "unknown");
            return Task.CompletedTask;
        };
    }

    /// <summary>Client ids of the connected gateways (for status reporting).</summary>
    public IReadOnlyCollection<string> ConnectedGateways => [.. _roles.Where(role => role.Value != ServiceRole).Select(role => role.Key)];

    public Task StartAsync() => _server.StartAsync();

    public Task StopAsync() => _server.StopAsync();

    /// <summary>Copy of every accepted gateway uplink, e.g. for logging in the broker service. Oldest dropped when full.</summary>
    public async IAsyncEnumerable<MqttUplink> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var uplink in _uplinks.Reader.ReadAllAsync(cancellationToken))
        {
            yield return uplink;
        }
    }

    /// <summary>Publishes a mesh packet as a ServiceEnvelope from inside this process (gateways with downlink transmit it).</summary>
    public async Task PublishPacketAsync(string root, string channel, string senderId, MeshPacket packet, CancellationToken cancellationToken)
    {
        var envelope = new ServiceEnvelope { Packet = packet, ChannelId = channel, GatewayId = senderId };
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(MeshtasticTopic.ForEnvelope(root, channel, senderId))
            .WithPayload(envelope.ToByteArray())
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build();
        await _server.InjectApplicationMessage(new InjectedMqttApplicationMessage(message) { SenderClientId = InternalClientId }, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _server.StopAsync();
        _server.Dispose();
        _uplinks.Writer.TryComplete();
    }

    private Task ValidateAsync(ValidatingConnectionEventArgs args)
    {
        var isService = args.UserName == _options.ServiceUserName && args.Password == _options.ServicePassword;
        var isGateway = !isService && args.UserName != _options.ServiceUserName
            && (_options.AllowAnyGateway || _authenticateGateway(args.ClientId, args.UserName, args.Password));

        if (isService || isGateway)
        {
            _roles[args.ClientId] = isService ? ServiceRole : "gateway";
            args.ReasonCode = MqttConnectReasonCode.Success;
        }
        else
        {
            args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            LogRejected(_logger, args.ClientId, args.UserName);
        }

        return Task.CompletedTask;
    }

    private Task InterceptPublishAsync(InterceptingPublishEventArgs args)
    {
        if (args.ClientId == InternalClientId || IsService(args.ClientId))
        {
            return Task.CompletedTask;
        }

        // A gateway: only Meshtastic topics, and not faster than the limit.
        var message = args.ApplicationMessage;
        if (!MeshtasticTopic.TryParse(message.Topic, out _))
        {
            args.ProcessPublish = false;
            LogIgnoredTopic(_logger, args.ClientId, message.Topic);
            return Task.CompletedTask;
        }

        if (!TryTakeToken(args.ClientId))
        {
            args.ProcessPublish = false;
            return Task.CompletedTask;
        }

        _uplinks.Writer.TryWrite(MqttUplink.From(_timeProvider.GetUtcNow(), args.ClientId, args.UserName, message.Topic, message.Payload.ToArray()));
        return Task.CompletedTask;
    }

    /// <summary>The routing rule: gateway traffic goes only to the service; gateways only get traffic from the service.</summary>
    private Task InterceptEnqueueAsync(InterceptingClientApplicationMessageEnqueueEventArgs args)
    {
        var fromService = args.SenderClientId == InternalClientId || IsService(args.SenderClientId);
        var toService = IsService(args.ReceiverClientId);
        args.AcceptEnqueue = fromService ? !toService || args.SenderClientId == InternalClientId : toService;
        return Task.CompletedTask;
    }

    private bool IsService(string? clientId) => clientId is not null && _roles.TryGetValue(clientId, out var role) && role == ServiceRole;

    private string RoleOf(string clientId) => _roles.TryGetValue(clientId, out var role) ? role : "unknown";

    /// <summary>Simple per-second window per gateway.</summary>
    private bool TryTakeToken(string clientId)
    {
        var second = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var window = _rates.AddOrUpdate(
            clientId,
            _ => new RateWindow(second, 1),
            (_, current) => current.Second == second ? current with { Count = current.Count + 1 } : new RateWindow(second, 1));

        if (window.Count <= _options.MaxMessagesPerSecondPerGateway)
        {
            return true;
        }

        if (window.Count == _options.MaxMessagesPerSecondPerGateway + 1)
        {
            LogRateLimited(_logger, clientId, _options.MaxMessagesPerSecondPerGateway);
        }

        return false;
    }

    private sealed record RateWindow(long Second, int Count);

    [LoggerMessage(Level = LogLevel.Information, Message = "MQTT client {ClientId} connected as {Role} (user {UserName}, from {RemoteEndPoint})")]
    private static partial void LogConnected(ILogger logger, string clientId, string role, string? userName, string? remoteEndPoint);

    [LoggerMessage(Level = LogLevel.Information, Message = "MQTT client {ClientId} disconnected ({Reason})")]
    private static partial void LogDisconnected(ILogger logger, string clientId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MQTT client {ClientId} rejected (user {UserName})")]
    private static partial void LogRejected(ILogger logger, string clientId, string? userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gateway {ClientId} published to a non-Meshtastic topic {Topic}; ignored")]
    private static partial void LogIgnoredTopic(ILogger logger, string clientId, string topic);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gateway {ClientId} exceeds {Limit} messages per second; extra messages are dropped")]
    private static partial void LogRateLimited(ILogger logger, string clientId, int limit);
}
