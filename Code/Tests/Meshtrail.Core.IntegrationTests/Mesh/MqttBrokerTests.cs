using System.Buffers;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Meshtrail.Mesh.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using MQTTnet.Formatter;
using Shouldly;

namespace Meshtrail.Core.IntegrationTests.Mesh;

/// <summary>
/// The Meshtrail MQTT broker with real MQTT clients: raw clients play gateways, <see cref="MeshtasticMqttClient"/>
/// plays the Meshtrail API. Loopback only; no database needed.
/// </summary>
[TestClass]
public sealed class MqttBrokerTests
{
    private const string Root = "msh/EU_868";
    private const string ServicePassword = "test-service-password";
    private const string DownlinkFilter = Root + "/2/e/LongFast/+";

    [TestMethod]
    public async Task GatewayUplink_ReachesTheService_ButNotOtherGateways()
    {
        // Arrange
        var (broker, port) = await StartBrokerAsync();
        await using var _ = broker;
        await using var service = await ConnectServiceAsync(port);
        using var gatewayA = await ConnectGatewayAsync(port, "a");
        using var gatewayB = await ConnectGatewayAsync(port, "b");
        var gatewayBReceived = Subscribe(gatewayB);
        await gatewayB.SubscribeAsync(DownlinkFilter);

        // Act
        await gatewayA.PublishAsync(Uplink("!0000000a", "hello"));
        var uplink = await FirstAsync(service.ReadAllAsync(Timeout()), message => message.Envelope is not null);

        // Assert
        uplink.Topic!.GatewayId.ShouldBe("!0000000a");
        uplink.UserName.ShouldBe("gw-a");
        uplink.Envelope!.Packet.Decoded.Payload.ToStringUtf8().ShouldBe("hello");
        (await Task.WhenAny(gatewayBReceived.Task, Task.Delay(1000))).ShouldNotBe(gatewayBReceived.Task);
    }

    [TestMethod]
    public async Task GatewayUplink_CannotPretendToBeAnotherGateway()
    {
        // Arrange: an MQTT 5 gateway sets the login property itself.
        var (broker, port) = await StartBrokerAsync();
        await using var _ = broker;
        await using var service = await ConnectServiceAsync(port);
        using var gateway = await ConnectGatewayAsync(port, "a");
        var forged = Uplink("!0000000a", "forged");
        forged.UserProperties = [new MQTTnet.Packets.MqttUserProperty(MeshtrailMqtt.GatewayProperty, System.Text.Encoding.UTF8.GetBytes("gw-victim"))];

        // Act
        await gateway.PublishAsync(forged);
        var uplink = await FirstAsync(service.ReadAllAsync(Timeout()), message => message.Envelope is not null);

        // Assert
        uplink.UserName.ShouldBe("gw-a");
    }

    [TestMethod]
    public async Task TargetedDownlink_ReachesOnlyThatGateway()
    {
        // Arrange
        var (broker, port) = await StartBrokerAsync();
        await using var _ = broker;
        await using var service = await ConnectServiceAsync(port);
        using var gatewayA = await ConnectGatewayAsync(port, "a");
        using var gatewayB = await ConnectGatewayAsync(port, "b");
        var receivedA = Subscribe(gatewayA);
        var receivedB = Subscribe(gatewayB);
        await gatewayA.SubscribeAsync(DownlinkFilter);
        await gatewayB.SubscribeAsync(DownlinkFilter);

        // Act
        await service.PublishPacketAsync(Root, "LongFast", "!4d545231", new MeshPacket { To = 0x0aa0_0001, Id = 9 }, "gw-b", CancellationToken.None);

        // Assert
        (await receivedB.Task.WaitAsync(TimeSpan.FromSeconds(5))).Topic.ShouldBe($"{Root}/2/e/LongFast/!4d545231");
        (await Task.WhenAny(receivedA.Task, Task.Delay(1000))).ShouldNotBe(receivedA.Task);
    }

    [TestMethod]
    public async Task GatewayConnects_ServiceGetsTheListOfConnectedLogins()
    {
        // Arrange
        var (broker, port) = await StartBrokerAsync();
        await using var _ = broker;
        await using var service = await ConnectServiceAsync(port);

        // Act
        using var gateway = await ConnectGatewayAsync(port, "a");
        GatewayConnections? connections = null;
        await foreach (var message in service.ReadAllAsync(Timeout()))
        {
            if (message.RawTopic == MeshtrailMqtt.ConnectionsTopic)
            {
                connections = MeshtrailMqtt.DeserializeConnections(message.Payload);
                break;
            }
        }

        // Assert
        connections.ShouldNotBeNull();
        connections.Broker.ShouldBe("local");
        connections.UserNames.ShouldBe(["gw-a"]);
    }

    [TestMethod]
    public async Task KnownGatewayLogin_IsAcceptedByTheAuthenticator()
    {
        // Arrange
        var (broker, port) = await StartBrokerAsync(allowAnyGateway: false, authenticate: login => login.UserName == "gw-ok" && login.Password == "whatever");
        await using var _ = broker;

        // Act
        using var gateway = await ConnectGatewayAsync(port, "ok");

        // Assert
        gateway.IsConnected.ShouldBeTrue();
    }

    [TestMethod]
    public async Task ServiceDownlink_ReachesTheGateways()
    {
        // Arrange
        var (broker, port) = await StartBrokerAsync();
        await using var _ = broker;
        await using var service = await ConnectServiceAsync(port);
        using var gateway = await ConnectGatewayAsync(port, "a");
        var received = Subscribe(gateway);
        await gateway.SubscribeAsync(DownlinkFilter);
        var packet = new MeshPacket { From = 0x4d54_5231, To = 0x0aa0_0001, Id = 7, WantAck = true };

        // Act
        await service.PublishPacketAsync(Root, "LongFast", "!4d545231", packet, null, CancellationToken.None);
        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        message.Topic.ShouldBe($"{Root}/2/e/LongFast/!4d545231");
        var envelope = ServiceEnvelope.Parser.ParseFrom(message.Payload.ToArray());
        envelope.Packet.To.ShouldBe(0x0aa0_0001u);
        envelope.Packet.WantAck.ShouldBeTrue();
    }

    [TestMethod]
    public async Task ServiceDownlink_IsAlsoVisibleToAnotherServiceLogin()
    {
        // Arrange: e.g. MQTT Explorer logged in with the service account, next to the API.
        var (broker, port) = await StartBrokerAsync();
        await using var _ = broker;
        await using var api = await ConnectServiceAsync(port);
        await using var observer = await ConnectServiceAsync(port);
        var packet = new MeshPacket { From = 0x4d54_5231, To = 0x0aa0_0001, Id = 8 };

        // Act
        await api.PublishPacketAsync(Root, "LongFast", "!4d545231", packet, null, CancellationToken.None);
        var seen = await FirstAsync(observer.ReadAllAsync(Timeout()), message => message.Envelope is not null);

        // Assert
        seen.Envelope!.Packet.Id.ShouldBe(8u);
    }

    [TestMethod]
    public async Task WrongServicePassword_IsRefused()
    {
        // Arrange
        var (broker, port) = await StartBrokerAsync();
        await using var _ = broker;
        await using var client = new MeshtasticMqttClient(
            new MeshtasticMqttClientOptions { Host = "127.0.0.1", Port = port, Password = "wrong" }, TimeProvider.System);

        // Act + Assert
        await Should.ThrowAsync<Exception>(async () => await client.ConnectAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task UnknownGateway_IsRefused_WhenAnyGatewayIsNotAllowed()
    {
        // Arrange
        var (broker, port) = await StartBrokerAsync(allowAnyGateway: false);
        await using var _ = broker;

        // Act + Assert
        await Should.ThrowAsync<Exception>(async () => await ConnectGatewayAsync(port, "intruder"));
    }

    [TestMethod]
    public async Task GatewayOverRateLimit_ExtraMessagesAreDropped()
    {
        // Arrange
        var (broker, port) = await StartBrokerAsync(maxPerSecond: 3);
        await using var _ = broker;
        await using var service = await ConnectServiceAsync(port);
        using var gateway = await ConnectGatewayAsync(port, "a");

        // Act
        for (var i = 0; i < 20; i++)
        {
            await gateway.PublishAsync(Uplink("!0000000a", $"m{i}"));
        }

        var received = await CountAsync(service.ReadAllAsync(Timeout(TimeSpan.FromSeconds(2))), message => message.Envelope is not null);

        // Assert: at most 3 per second; the burst may straddle a second boundary.
        received.ShouldBeInRange(1, 6);
    }

    // ---- helpers

    private static MqttApplicationMessage Uplink(string gatewayId, string text) => new MqttApplicationMessageBuilder()
        .WithTopic($"{Root}/2/e/LongFast/{gatewayId}")
        .WithPayload(new ServiceEnvelope
        {
            ChannelId = "LongFast",
            GatewayId = gatewayId,
            Packet = new MeshPacket
            {
                From = 0x0aa0_0001,
                To = uint.MaxValue,
                Id = (uint)Random.Shared.Next(1, int.MaxValue),
                Decoded = new Data { Portnum = PortNum.TextMessageApp, Payload = ByteString.CopyFromUtf8(text) },
            },
        }.ToByteArray())
        .Build();

    private static async Task<(MeshtasticMqttBroker Broker, int Port)> StartBrokerAsync(
        bool allowAnyGateway = true, int maxPerSecond = 20, Func<GatewayLogin, bool>? authenticate = null)
    {
        var port = FreePort();
        var broker = new MeshtasticMqttBroker(
            new MeshtasticMqttBrokerOptions
            {
                Port = port,
                BindAddress = "127.0.0.1",
                ServicePassword = ServicePassword,
                AllowAnyGateway = allowAnyGateway,
                MaxMessagesPerSecondPerGateway = maxPerSecond,
            },
            (login, _) => Task.FromResult(authenticate?.Invoke(login) ?? false),
            TimeProvider.System,
            NullLogger.Instance);
        await broker.StartAsync();
        return (broker, port);
    }

    private static async Task<MeshtasticMqttClient> ConnectServiceAsync(int port)
    {
        var client = new MeshtasticMqttClient(
            new MeshtasticMqttClientOptions { Host = "127.0.0.1", Port = port, Password = ServicePassword }, TimeProvider.System);
        await client.ConnectAsync(CancellationToken.None);
        return client;
    }

    private static async Task<IMqttClient> ConnectGatewayAsync(int port, string name)
    {
        var client = new MqttClientFactory().CreateMqttClient();
        // MQTT 5, so a test can try to set user properties itself (real gateways use 3.1.1).
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("127.0.0.1", port)
            .WithClientId($"gateway-{name}-{Guid.NewGuid():N}")
            .WithCredentials($"gw-{name}", "whatever")
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .Build();
        var result = await client.ConnectAsync(options);
        if (result.ResultCode != MqttClientConnectResultCode.Success)
        {
            client.Dispose();
            throw new InvalidOperationException($"Connect refused: {result.ResultCode}");
        }

        return client;
    }

    private static TaskCompletionSource<MqttApplicationMessage> Subscribe(IMqttClient client)
    {
        var received = new TaskCompletionSource<MqttApplicationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ApplicationMessageReceivedAsync += args =>
        {
            received.TrySetResult(args.ApplicationMessage);
            return Task.CompletedTask;
        };
        return received;
    }

    private static CancellationToken Timeout(TimeSpan? after = null) => new CancellationTokenSource(after ?? TimeSpan.FromSeconds(5)).Token;

    /// <summary>The first message that matches (the service also gets the broker's connection lists).</summary>
    private static async Task<MqttUplink> FirstAsync(IAsyncEnumerable<MqttUplink> uplinks, Func<MqttUplink, bool> match)
    {
        await foreach (var uplink in uplinks)
        {
            if (match(uplink))
            {
                return uplink;
            }
        }

        throw new TimeoutException("No uplink received.");
    }

    private static async Task<int> CountAsync(IAsyncEnumerable<MqttUplink> uplinks, Func<MqttUplink, bool> match)
    {
        var count = 0;
        try
        {
            await foreach (var uplink in uplinks)
            {
                count += match(uplink) ? 1 : 0;
            }
        }
        catch (OperationCanceledException)
        {
            // The time window is over.
        }

        return count;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
