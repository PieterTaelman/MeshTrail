using Meshtastic.Protobufs;
using Meshtrail.Mesh.Events;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

[TestClass]
public sealed class PacketTranslatorTests
{
    [TestMethod]
    public void Translate_MyInfo_RemembersGatewayAndReportsIt()
    {
        // Arrange
        var translator = new PacketTranslator();

        // Act
        var events = translator.Translate(new FromRadio { MyInfo = new MyNodeInfo { MyNodeNum = GatewayNodeNum } }, Now);

        // Assert
        translator.GatewayNodeNum.ShouldBe(GatewayNodeNum);
        events.ShouldHaveSingleItem().ShouldBe(new GatewayInfoReceived(Now, GatewayNodeNum, null));
    }

    [TestMethod]
    public void Translate_NodeInfo_ReturnsUserPositionTelemetryAndLastHeard()
    {
        // Arrange
        var translator = new PacketTranslator();
        var message = new FromRadio
        {
            NodeInfo = new NodeInfo
            {
                Num = HikerNodeNum,
                User = new User { LongName = "Hiker", ShortName = "HKR", HwModel = HardwareModel.M5StackC6L },
                Position = PositionAt(50.35, 5.45),
                DeviceMetrics = new DeviceMetrics { BatteryLevel = 80, Voltage = 3.9f },
                LastHeard = 1_790_000_000,
                Snr = 7.5f,
                HopsAway = 2,
            },
        };

        // Act
        var nodeInfo = translator.Translate(message, Now).ShouldHaveSingleItem().ShouldBeOfType<NodeInfoReceived>();

        // Assert
        nodeInfo.NodeNum.ShouldBe(HikerNodeNum);
        nodeInfo.User!.LongName.ShouldBe("Hiker");
        nodeInfo.User.HardwareModel.ShouldBe("M5StackC6L");
        nodeInfo.Position!.Latitude.ShouldBe(50.35, 0.000001);
        nodeInfo.Position.Altitude.ShouldBe(250);
        nodeInfo.Telemetry!.BatteryLevel.ShouldBe(80);
        nodeInfo.LastHeardAt.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1_790_000_000));
        nodeInfo.Snr.ShouldBe(7.5);
        nodeInfo.HopsAway.ShouldBe(2);
    }

    [TestMethod]
    public void Translate_NodeInfoNeverHeard_HasNoLastHeard()
    {
        // Arrange
        var translator = new PacketTranslator();

        // Act
        var nodeInfo = translator.Translate(new FromRadio { NodeInfo = new NodeInfo { Num = HikerNodeNum } }, Now)
            .ShouldHaveSingleItem().ShouldBeOfType<NodeInfoReceived>();

        // Assert
        nodeInfo.LastHeardAt.ShouldBeNull();
        nodeInfo.HopsAway.ShouldBeNull();
        nodeInfo.Position.ShouldBeNull();
    }

    [TestMethod]
    public void Translate_PositionPacket_ReturnsHeardAndPosition()
    {
        // Arrange
        var translator = new PacketTranslator();

        // Act
        var events = translator.Translate(Packet(HikerNodeNum, PortNum.PositionApp, PositionAt(49.79, 5.07)), Now);

        // Assert
        events.Count.ShouldBe(2);
        events[0].ShouldBe(new NodeHeard(Now, HikerNodeNum, 6.25, -71, 2));
        var position = events[1].ShouldBeOfType<PositionReceived>().Position;
        position.Longitude.ShouldBe(5.07, 0.000001);
        position.Time.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1_790_000_000));
    }

    [TestMethod]
    public void Translate_PositionWithoutFix_ReturnsOnlyHeard()
    {
        // Arrange
        var translator = new PacketTranslator();

        // Act
        var events = translator.Translate(Packet(HikerNodeNum, PortNum.PositionApp, new Position { LatitudeI = 0, LongitudeI = 0 }), Now);

        // Assert
        events.ShouldHaveSingleItem().ShouldBeOfType<NodeHeard>();
    }

    [TestMethod]
    public void Translate_PacketFromGateway_HasNoSignalValues()
    {
        // Arrange
        var translator = new PacketTranslator();
        translator.Translate(new FromRadio { MyInfo = new MyNodeInfo { MyNodeNum = GatewayNodeNum } }, Now);
        var telemetry = new Telemetry { DeviceMetrics = new DeviceMetrics { BatteryLevel = 101, Voltage = 0 } };

        // Act
        var events = translator.Translate(Packet(GatewayNodeNum, PortNum.TelemetryApp, telemetry), Now);

        // Assert
        var heard = events[0].ShouldBeOfType<NodeHeard>();
        heard.Snr.ShouldBeNull();
        heard.Rssi.ShouldBeNull();
        events[1].ShouldBe(new TelemetryReceived(Now, GatewayNodeNum, new DeviceTelemetry(101, null)));
    }

    [TestMethod]
    public void Translate_TracerouteAnswer_ConvertsSnrToDecibel()
    {
        // Arrange
        var translator = new PacketTranslator();
        var route = new RouteDiscovery { Route = { 11u }, SnrTowards = { 24, -128 }, RouteBack = { 11u }, SnrBack = { -8 } };

        // Act
        var events = translator.Translate(Packet(HikerNodeNum, PortNum.TracerouteApp, route, requestId: 99), Now);

        // Assert
        var result = events[1].ShouldBeOfType<TracerouteReceived>();
        result.RequestId.ShouldBe(99u);
        result.RouteTowards.ShouldBe([11u]);
        result.SnrTowards.ShouldBe([6.0, null]);
        result.SnrBack.ShouldBe([-2.0]);
    }

    [TestMethod]
    public void Translate_TracerouteRequestPassingThrough_IsNotAResult()
    {
        // Arrange
        var translator = new PacketTranslator();

        // Act
        var events = translator.Translate(Packet(HikerNodeNum, PortNum.TracerouteApp, new RouteDiscovery()), Now);

        // Assert
        events.ShouldHaveSingleItem().ShouldBeOfType<NodeHeard>();
    }

    [TestMethod]
    public void Translate_EncryptedPacket_StillCountsAsHeard()
    {
        // Arrange
        var translator = new PacketTranslator();
        var message = new FromRadio { Packet = new MeshPacket { From = HikerNodeNum, Encrypted = Google.Protobuf.ByteString.CopyFrom(1, 2, 3) } };

        // Act
        var events = translator.Translate(message, Now);

        // Assert
        events.ShouldHaveSingleItem().ShouldBeOfType<NodeHeard>().HopsAway.ShouldBeNull();
    }

    [TestMethod]
    public void Translate_CorruptPayload_ReturnsOnlyHeard()
    {
        // Arrange
        var translator = new PacketTranslator();
        var message = Packet(HikerNodeNum, PortNum.PositionApp, new Position());
        message.Packet.Decoded.Payload = Google.Protobuf.ByteString.CopyFrom(0xFF, 0xFF, 0xFF);

        // Act
        var events = translator.Translate(message, Now);

        // Assert
        events.ShouldHaveSingleItem().ShouldBeOfType<NodeHeard>();
    }

    [TestMethod]
    public void Translate_ChannelConfig_IsIgnored()
    {
        // Arrange
        var translator = new PacketTranslator();
        var message = new FromRadio { Channel = new Channel { Index = 0, Settings = new ChannelSettings { Psk = Google.Protobuf.ByteString.CopyFrom(1) } } };

        // Act
        var events = translator.Translate(message, Now);

        // Assert
        events.ShouldBeEmpty();
    }
}
