using Google.Protobuf;
using Mediator;
using Meshtastic.Protobufs;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Mesh.Framing;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Meshtrail.Core.UnitTests.Mesh;

/// <summary>Shared test data for the Mesh tests: the real gateway node, frame builders, domain objects and mocks.</summary>
internal static class MeshTestHelpers
{
    /// <summary>The project's real gateway node (M5Stack C6L "Node 2109").</summary>
    public const uint GatewayNodeNum = 4044729068;
    public const string GatewayNodeId = "!f115aaec";

    /// <summary>The hiker node used in the examples.</summary>
    public const uint HikerNodeNum = 0x0aa0_0001;

    public const string UserName = "test-user";

    /// <summary>
    /// Contact link for the gateway as the Meshtastic app encodes it (base64url, no padding).
    /// Built by hand from the protobuf wire format, so it does not depend on our own encoder.
    /// Public key = bytes 1..32.
    /// </summary>
    public const string GatewayContactUrl =
        "https://meshtastic.org/v/#COzV1ogPEkAKCSFmMTE1YWFlYxIJTm9kZSAyMTA5GgQyMTA5KG9CIAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8g";

    public static readonly DateTimeOffset Now = new(2026, 10, 3, 20, 0, 0, TimeSpan.Zero);

    public static byte[] GatewayPublicKey => [.. Enumerable.Range(1, 32).Select(i => (byte)i)];

    public static SharedContact GatewayContact() => new()
    {
        NodeNum = GatewayNodeNum,
        User = new User
        {
            Id = GatewayNodeId,
            LongName = "Node 2109",
            ShortName = "2109",
            HwModel = HardwareModel.M5StackC6L,
            PublicKey = ByteString.CopyFrom(GatewayPublicKey),
        },
    };

    // ---- framing

    /// <summary>A small FromRadio frame (header + payload) for framing tests.</summary>
    public static byte[] Frame(uint configCompleteId) => FrameWriter.Encode(new FromRadio { ConfigCompleteId = configCompleteId });

    public static uint ConfigCompleteId(byte[] payload) => FromRadio.Parser.ParseFrom(payload).ConfigCompleteId;

    // ---- radio packets

    public static FromRadio Packet(uint from, PortNum port, Google.Protobuf.IMessage payload, uint requestId = 0) => new()
    {
        Packet = new MeshPacket
        {
            From = from,
            To = GatewayNodeNum,
            Id = 1234,
            RxSnr = 6.25f,
            RxRssi = -71,
            HopStart = 3,
            HopLimit = 1,
            Decoded = new Data { Portnum = port, Payload = payload.ToByteString(), RequestId = requestId },
        },
    };

    public static Position PositionAt(double latitude, double longitude, uint time = 1_790_000_000) =>
        new() { LatitudeI = (int)(latitude * 1e7), LongitudeI = (int)(longitude * 1e7), Altitude = 250, Time = time, PrecisionBits = 32 };

    // ---- domain

    public static FakeTimeProvider FixedTime() => new(Now);

    public static GeoPosition Fix(double latitude = 50.85, double longitude = 4.35, DateTimeOffset? time = null) =>
        GeoPosition.Create(latitude, longitude, 100, time ?? Now, 32);

    public static MeshNode KnownNode(uint nodeNum = HikerNodeNum) => MeshNode.Discover(nodeNum, Now.AddDays(-1));

    public static MeshGateway Gateway(GatewayStatus status)
    {
        var gateway = MeshGateway.Register(MeshGateway.PrimaryKey, "Tcp", Now.AddHours(-1));
        gateway.ChangeStatus(status, "Tcp", status == GatewayStatus.Online ? null : "test", Now.AddHours(-1));
        gateway.Identify(GatewayNodeNum, "2.7.26");
        return gateway;
    }

    // ---- mocks

    public static Mock<IMeshNodeRepository> NodesReturning(MeshNode? node)
    {
        var nodes = new Mock<IMeshNodeRepository>();
        nodes.Setup(repo => repo.GetAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>())).ReturnsAsync(node);
        return nodes;
    }

    public static Mock<IMeshGatewayRepository> GatewaysReturning(MeshGateway? gateway)
    {
        var gateways = new Mock<IMeshGatewayRepository>();
        gateways.Setup(repo => repo.GetAsync(MeshGateway.PrimaryKey, It.IsAny<CancellationToken>())).ReturnsAsync(gateway);
        return gateways;
    }

    public static Mock<IMeshGateway> MeshGatewayPort(uint packetId = 777)
    {
        var port = new Mock<IMeshGateway>();
        port.Setup(gateway => gateway.NewPacketId()).Returns(packetId);
        return port;
    }

    public static Mock<ICurrentUser> CurrentUser()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Name).Returns(UserName);
        return currentUser;
    }

    public static Mock<IPublisher> Publisher() => new();
}
