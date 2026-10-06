using System.Buffers;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Meshtrail.Mesh.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using Shouldly;

namespace Meshtrail.Core.IntegrationTests.Mesh;

/// <summary>
/// The embedded broker with a real MQTT client acting like a Meshtastic gateway (loopback only, no database).
/// </summary>
[TestClass]
public sealed class MqttBrokerTests
{
    private const string Root = "msh/EU_868";
    private const string GatewayId = "!f115aaec";

    [TestMethod]
    public async Task GatewayPublishesEnvelope_BrokerHandsItToUsDecoded()
    {
        // Arrange
        var (broker, port) = await StartBrokerAsync();
        await using var _ = broker;
        using var gateway = await ConnectGatewayAsync(port, "gw-user", "secret");
        var envelope = new ServiceEnvelope
        {
            ChannelId = "LongFast",
            GatewayId = GatewayId,
            Packet = new MeshPacket
            {
                From = 0x0aa0_0001,
                To = uint.MaxValue,
                Id = 42,
                Decoded = new Data { Portnum = PortNum.TextMessageApp, Payload = ByteString.CopyFromUtf8("hello") },
            },
        };

        // Act
        await gateway.PublishAsync(new MqttApplicationMessageBuilder()
            .WithTopic($"{Root}/2/e/LongFast/{GatewayId}")
            .WithPayload(envelope.ToByteArray())
            .Build());
        var uplink = await FirstUplinkAsync(broker);

        // Assert
        uplink.UserName.ShouldBe("gw-user");
        uplink.Topic!.Channel.ShouldBe("LongFast");
        uplink.Topic.GatewayId.ShouldBe(GatewayId);
        uplink.Envelope!.Packet.Decoded.Payload.ToStringUtf8().ShouldBe("hello");
    }

    [TestMethod]
    public async Task WePublishPacket_SubscribedGatewayReceivesEnvelope()
    {
        // Arrange
        var (broker, port) = await StartBrokerAsync();
        await using var _ = broker;
        using var gateway = await ConnectGatewayAsync(port, "gw-user", "secret");
        var received = new TaskCompletionSource<MqttApplicationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.ApplicationMessageReceivedAsync += args =>
        {
            received.TrySetResult(args.ApplicationMessage);
            return Task.CompletedTask;
        };
        await gateway.SubscribeAsync($"{Root}/2/e/LongFast/+");
        var packet = new MeshPacket { From = 0x4d54_5231, To = 0x0aa0_0001, Id = 7, WantAck = true };

        // Act
        await broker.PublishPacketAsync(Root, "LongFast", "!4d545231", packet, CancellationToken.None);
        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        message.Topic.ShouldBe($"{Root}/2/e/LongFast/!4d545231");
        var envelope = ServiceEnvelope.Parser.ParseFrom(message.Payload.ToArray());
        envelope.GatewayId.ShouldBe("!4d545231");
        envelope.Packet.To.ShouldBe(0x0aa0_0001u);
        envelope.Packet.WantAck.ShouldBeTrue();
    }

    [TestMethod]
    public async Task WrongPassword_ConnectionIsRefused()
    {
        // Arrange
        var (broker, port) = await StartBrokerAsync();
        await using var _ = broker;

        // Act + Assert
        await Should.ThrowAsync<Exception>(async () => await ConnectGatewayAsync(port, "gw-user", "wrong"));
    }

    private static async Task<(MeshtasticMqttBroker Broker, int Port)> StartBrokerAsync()
    {
        var port = FreePort();
        var broker = new MeshtasticMqttBroker(
            new MeshtasticMqttBrokerOptions { Port = port, BindAddress = IPAddress.Loopback },
            (_, user, password) => user == "gw-user" && password == "secret",
            TimeProvider.System,
            NullLogger.Instance);
        await broker.StartAsync();
        return (broker, port);
    }

    private static async Task<IMqttClient> ConnectGatewayAsync(int port, string user, string password)
    {
        var client = new MqttClientFactory().CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("127.0.0.1", port)
            .WithClientId($"gateway-{Guid.NewGuid():N}")
            .WithCredentials(user, password)
            .Build();
        var result = await client.ConnectAsync(options);
        if (result.ResultCode != MqttClientConnectResultCode.Success)
        {
            client.Dispose();
            throw new InvalidOperationException($"Connect refused: {result.ResultCode}");
        }

        return client;
    }

    private static async Task<MqttUplink> FirstUplinkAsync(MeshtasticMqttBroker broker)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var uplink in broker.ReadAllAsync(timeout.Token))
        {
            return uplink;
        }

        throw new TimeoutException("No uplink received.");
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
