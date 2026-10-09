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
using MQTTnet.Packets;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace Meshtrail.Mesh.Mqtt;

/// <summary>Settings of the Meshtrail MQTT broker (section MqttBroker).</summary>
public sealed class MeshtasticMqttBrokerOptions
{
    public const string SectionName = "MqttBroker";

    /// <summary>Name of this broker (region), e.g. "local" or "eu-west". Gateways are linked to the broker they use.</summary>
    public string Name { get; set; } = "local";

    /// <summary>Plain MQTT port. TLS (8883) comes with production hosting.</summary>
    public int Port { get; set; } = 1883;

    /// <summary>Address to listen on; 0.0.0.0 = every network card, so nodes on the network can reach it.</summary>
    public string BindAddress { get; set; } = "0.0.0.0";

    /// <summary>Login of the Meshtrail API. Only this client receives gateway traffic and may send to gateways.</summary>
    public string ServiceUserName { get; set; } = "meshtrail-api";

    /// <summary>Password of the service login. A secret: user secrets / environment, never in git (except dev defaults).</summary>
    public string ServicePassword { get; set; } = string.Empty;

    /// <summary>Troubleshooting only: accept any gateway login without asking the API.</summary>
    public bool AllowAnyGateway { get; set; }

    /// <summary>Messages per second one gateway may publish; more is dropped (protects the platform from broken nodes).</summary>
    public int MaxMessagesPerSecondPerGateway { get; set; } = 20;

    /// <summary>How often the list of connected gateways is sent to the API (it is also sent on every connect/disconnect).</summary>
    public TimeSpan ConnectionsInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Write a one-line summary of every uplink to the log (development / troubleshooting).</summary>
    public bool LogUplinks { get; set; }
}

/// <summary>A gateway trying to log in. Never log the password.</summary>
public sealed record GatewayLogin(string ClientId, string? UserName, string? Password);

/// <summary>
/// The broker Meshtastic gateways connect to (their MQTT module). It is a strict router, not a free-for-all broker:
/// <list type="bullet">
/// <item>gateways publish uplinks; only service logins (the Meshtrail API, or a tool like MQTT Explorer logged in with
/// the service account) receive them. Each uplink is stamped with the gateway's login;</item>
/// <item>only service logins (or this process) may send to gateways — a gateway never receives another gateway's
/// traffic, otherwise every gateway would re-transmit everything it got from MQTT over the air. A downlink with a
/// target goes to that one gateway only;</item>
/// <item>each gateway is rate-limited.</item>
/// </list>
/// </summary>
public sealed partial class MeshtasticMqttBroker : IAsyncDisposable
{
    private const string InternalClientId = "meshtrail-broker";

    private readonly MeshtasticMqttBrokerOptions _options;
    private readonly MqttServer _server;
    private readonly Channel<MqttUplink> _uplinks = System.Threading.Channels.Channel.CreateBounded<MqttUplink>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly ConcurrentDictionary<string, ConnectedClient> _clients = new();
    private readonly ConcurrentDictionary<string, RateWindow> _rates = new();
    private readonly Func<GatewayLogin, CancellationToken, Task<bool>> _authenticateGateway;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private ITimer? _connectionsTimer;

    /// <param name="authenticateGateway">Is this a known, active gateway login? (The broker service asks the API.)</param>
    public MeshtasticMqttBroker(
        MeshtasticMqttBrokerOptions options,
        Func<GatewayLogin, CancellationToken, Task<bool>> authenticateGateway,
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
        _server.ClientConnectedAsync += OnConnectedAsync;
        _server.ClientDisconnectedAsync += OnDisconnectedAsync;
    }

    /// <summary>Logins of the connected gateways (for status reporting).</summary>
    public IReadOnlyList<string> ConnectedGateways =>
        [.. _clients.Values.Where(client => !client.IsService).Select(client => client.Login).Distinct().Order(StringComparer.Ordinal)];

    public async Task StartAsync()
    {
        await _server.StartAsync();
        _connectionsTimer = _timeProvider.CreateTimer(
            _ => _ = PublishConnectionsQuietlyAsync(), null, _options.ConnectionsInterval, _options.ConnectionsInterval);
    }

    public async Task StopAsync()
    {
        if (_connectionsTimer is not null)
        {
            await _connectionsTimer.DisposeAsync();
            _connectionsTimer = null;
        }

        await _server.StopAsync();
    }

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

    /// <summary>Tells the service logins which gateways are connected (also done on a timer and on every connect/disconnect).</summary>
    public async Task PublishConnectionsAsync(CancellationToken cancellationToken)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(MeshtrailMqtt.ConnectionsTopic)
            .WithPayload(MeshtrailMqtt.SerializeConnections(new GatewayConnections(_options.Name, ConnectedGateways)))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
            .Build();
        await _server.InjectApplicationMessage(new InjectedMqttApplicationMessage(message) { SenderClientId = InternalClientId }, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _server.Dispose();
        _uplinks.Writer.TryComplete();
    }

    private async Task ValidateAsync(ValidatingConnectionEventArgs args)
    {
        var isService = args.UserName == _options.ServiceUserName && args.Password == _options.ServicePassword;
        var isGateway = false;
        if (!isService && args.UserName != _options.ServiceUserName)
        {
            try
            {
                isGateway = _options.AllowAnyGateway
                    || await _authenticateGateway(new GatewayLogin(args.ClientId, args.UserName, args.Password), CancellationToken.None);
            }
            catch (Exception exception)
            {
                // Fail closed: when we cannot check the login, the gateway tries again later.
                LogAuthenticationFailed(_logger, exception, args.ClientId);
            }
        }

        if (isService || isGateway)
        {
            _clients[args.ClientId] = new ConnectedClient(isService, string.IsNullOrEmpty(args.UserName) ? args.ClientId : args.UserName);
            args.ReasonCode = MqttConnectReasonCode.Success;
        }
        else
        {
            args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            LogRejected(_logger, args.ClientId, args.UserName);
        }
    }

    private Task OnConnectedAsync(ClientConnectedEventArgs args)
    {
        var client = ClientOf(args.ClientId);
        LogConnected(_logger, args.ClientId, client?.IsService == true ? "service" : "gateway", args.UserName, args.RemoteEndPoint?.ToString());
        return client is { IsService: false } ? PublishConnectionsQuietlyAsync() : Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(ClientDisconnectedEventArgs args)
    {
        _clients.TryRemove(args.ClientId, out var client);
        _rates.TryRemove(args.ClientId, out _);
        LogDisconnected(_logger, args.ClientId, args.ReasonCode?.ToString() ?? args.DisconnectType.ToString());
        return client is { IsService: false } ? PublishConnectionsQuietlyAsync() : Task.CompletedTask;
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

        // Stamp the login, so the API knows which registered gateway this is. A property the gateway set itself is
        // removed first: nobody can pretend to be another gateway.
        var login = ClientOf(args.ClientId)?.Login ?? args.ClientId;
        message.UserProperties ??= [];
        message.UserProperties.RemoveAll(property => property.Name is MeshtrailMqtt.GatewayProperty or MeshtrailMqtt.TargetProperty);
        message.UserProperties.Add(new MqttUserProperty(MeshtrailMqtt.GatewayProperty, Encoding.UTF8.GetBytes(login)));

        _uplinks.Writer.TryWrite(MqttUplink.From(_timeProvider.GetUtcNow(), args.ClientId, login, message.Topic, message.Payload.ToArray()));
        return Task.CompletedTask;
    }

    /// <summary>
    /// The routing rule: everything is delivered except gateway → gateway. So service logins see all traffic
    /// (uplinks and downlinks, handy for MQTT Explorer); gateways only get Meshtastic topics a service login sent,
    /// and only the targeted gateway when the downlink names one.
    /// </summary>
    private Task InterceptEnqueueAsync(InterceptingClientApplicationMessageEnqueueEventArgs args)
    {
        if (IsService(args.ReceiverClientId))
        {
            args.AcceptEnqueue = true;
            return Task.CompletedTask;
        }

        var fromService = args.SenderClientId == InternalClientId || IsService(args.SenderClientId);
        var message = args.ApplicationMessage;
        var target = MeshtrailMqtt.GetProperty(message, MeshtrailMqtt.TargetProperty);
        args.AcceptEnqueue = fromService
            && MeshtasticTopic.TryParse(message.Topic, out _)
            && (target is null || ClientOf(args.ReceiverClientId)?.Login == target);
        return Task.CompletedTask;
    }

    private async Task PublishConnectionsQuietlyAsync()
    {
        try
        {
            await PublishConnectionsAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            // Stopping, or nobody listening: the next timer tick tries again.
            LogConnectionsNotPublished(_logger, exception);
        }
    }

    private ConnectedClient? ClientOf(string? clientId) => clientId is not null && _clients.TryGetValue(clientId, out var client) ? client : null;

    private bool IsService(string? clientId) => ClientOf(clientId)?.IsService == true;

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

    private sealed record ConnectedClient(bool IsService, string Login);

    private sealed record RateWindow(long Second, int Count);

    [LoggerMessage(Level = LogLevel.Information, Message = "MQTT client {ClientId} connected as {Role} (user {UserName}, from {RemoteEndPoint})")]
    private static partial void LogConnected(ILogger logger, string clientId, string role, string? userName, string? remoteEndPoint);

    [LoggerMessage(Level = LogLevel.Information, Message = "MQTT client {ClientId} disconnected ({Reason})")]
    private static partial void LogDisconnected(ILogger logger, string clientId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MQTT client {ClientId} rejected (user {UserName})")]
    private static partial void LogRejected(ILogger logger, string clientId, string? userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not check the login of MQTT client {ClientId}; refused")]
    private static partial void LogAuthenticationFailed(ILogger logger, Exception exception, string clientId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gateway {ClientId} published to a non-Meshtastic topic {Topic}; ignored")]
    private static partial void LogIgnoredTopic(ILogger logger, string clientId, string topic);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gateway {ClientId} exceeds {Limit} messages per second; extra messages are dropped")]
    private static partial void LogRateLimited(ILogger logger, string clientId, int limit);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not publish the gateway connections")]
    private static partial void LogConnectionsNotPublished(ILogger logger, Exception exception);
}
