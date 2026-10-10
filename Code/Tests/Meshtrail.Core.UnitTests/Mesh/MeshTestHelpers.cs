using Google.Protobuf;
using Mediator;
using Meshtastic.Protobufs;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Domain.Teams;
using Team = Meshtrail.Core.Domain.Teams.Team;
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

    /// <summary>A second gateway somewhere else.</summary>
    public const uint OtherGatewayNodeNum = 0x0bb0_0002;

    /// <summary>Sender number of our MQTT packets ("MTR1").</summary>
    public const uint VirtualNodeNum = 0x4D54_5231;

    public const string GatewayLogin = "gw-test12345";
    public const string GatewayPassword = "Secret-Password-1234";

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

    public static FromRadio Packet(uint from, PortNum port, Google.Protobuf.IMessage payload, uint requestId = 0, uint id = 1234) => new()
    {
        Packet = new MeshPacket
        {
            From = from,
            To = GatewayNodeNum,
            Id = id,
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

    /// <summary>An MQTT gateway of <see cref="UserName"/> on node <paramref name="nodeNum"/>, credentials issued but no uplink yet.</summary>
    public static MeshGateway PendingGateway(string owner = UserName, string login = GatewayLogin, uint nodeNum = GatewayNodeNum) =>
        MeshGateway.IssueMqtt(owner, owner, nodeNum, "local", login, GatewayPassword, Now.AddHours(-1));

    /// <summary>An MQTT gateway tied to <paramref name="nodeNum"/> (online, or offline when <paramref name="online"/> is false).</summary>
    public static MeshGateway BoundGateway(uint nodeNum = GatewayNodeNum, bool online = true, string login = GatewayLogin)
    {
        var gateway = PendingGateway(login: login, nodeNum: nodeNum);
        gateway.Bind(nodeNum, Now.AddHours(-1));
        gateway.RecordUplink("msh/EU_868", "LongFast", Now.AddHours(-1));
        if (!online)
        {
            gateway.ChangeConnection(false, "test", Now.AddMinutes(-30));
        }

        return gateway;
    }

    /// <summary>"Gateway heard the hiker <paramref name="minutesAgo"/> minutes ago".</summary>
    public static NodeReception Heard(
        uint gatewayNodeNum = GatewayNodeNum, double minutesAgo = 5, int? hops = 1, double? snr = 5, uint nodeNum = HikerNodeNum) =>
        NodeReception.Record(nodeNum, gatewayNodeNum, Now.AddMinutes(-minutesAgo), snr, -80, hops, Now);

    public static GatewayRoute Route(uint gatewayNodeNum = GatewayNodeNum) =>
        new(gatewayNodeNum, GatewayTransport.Mqtt, "local", GatewayLogin, "msh/EU_868", "LongFast");

    // ---- mocks

    public static Mock<IMeshNodeRepository> NodesReturning(MeshNode? node)
    {
        var nodes = new Mock<IMeshNodeRepository>();
        nodes.Setup(repo => repo.GetAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>())).ReturnsAsync(node);
        nodes.Setup(repo => repo.GetManyAsync(It.IsAny<IReadOnlyCollection<uint>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<uint> nodeNums, CancellationToken _) => node is not null && nodeNums.Contains(node.NodeNum) ? [node] : []);
        return nodes;
    }

    /// <summary>A gateway repository that knows exactly these gateways.</summary>
    public static Mock<IMeshGatewayRepository> GatewaysWith(params MeshGateway[] known)
    {
        var gateways = new Mock<IMeshGatewayRepository>();
        gateways.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => known.FirstOrDefault(gateway => gateway.Id == id));
        gateways.Setup(repo => repo.GetActiveByNodeNumAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((uint nodeNum, CancellationToken _) => known.FirstOrDefault(gateway => gateway.IsActive && gateway.NodeNum == nodeNum));
        gateways.Setup(repo => repo.GetByMqttUserNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string login, CancellationToken _) => known.FirstOrDefault(gateway => gateway.MqttUserName == login));
        gateways.Setup(repo => repo.GetActiveByNodeNumsAsync(It.IsAny<IReadOnlyCollection<uint>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<uint> nodeNums, CancellationToken _) =>
                [.. known.Where(gateway => gateway is { IsActive: true, IsPending: false } && gateway.NodeNum is { } nodeNum && nodeNums.Contains(nodeNum))]);
        gateways.Setup(repo => repo.GetBoundAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. known.Where(gateway => gateway.IsActive && gateway.NodeNum is not null)]);
        gateways.Setup(repo => repo.GetActiveMqttOnBrokerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. known.Where(gateway => gateway.IsActive && gateway.Transport == GatewayTransport.Mqtt)]);
        gateways.Setup(repo => repo.GetCarryingChannelAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. known.Where(gateway => gateway.IsActive && gateway.NodeNum is not null)]);
        gateways.Setup(repo => repo.GetChannelsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<string>>());
        return gateways;
    }

    /// <summary>A team repository that knows exactly these teams.</summary>
    public static Mock<ITeamRepository> TeamsWith(params Team[] known)
    {
        var teams = new Mock<ITeamRepository>();
        teams.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => known.FirstOrDefault(team => team.Id == id));
        teams.Setup(repo => repo.GetByChannelNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string channel, CancellationToken _) => known.FirstOrDefault(team => team.ChannelName == channel));
        teams.Setup(repo => repo.GetByJoinCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string code, CancellationToken _) => known.FirstOrDefault(team => team.JoinCode == code));
        teams.Setup(repo => repo.ChannelNameTakenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string channel, CancellationToken _) => known.Any(team => team.ChannelName == channel));
        teams.Setup(repo => repo.GetMemberIdsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => [.. known.Where(team => team.Id == id).SelectMany(team => team.Members).Select(member => member.UserId)]);
        return teams;
    }

    /// <summary>A team of <see cref="UserName"/> on channel "Alpha".</summary>
    public static Team AlphaTeam(string owner = UserName) => Team.Create("Alpha", "Alpha", "JOINCODE", owner, owner, Now.AddDays(-1));

    public static Mock<INodeReceptionRepository> ReceptionsOf(params NodeReception[] receptions)
    {
        var repository = new Mock<INodeReceptionRepository>();
        repository.Setup(repo => repo.GetForNodeAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((uint nodeNum, CancellationToken _) => [.. receptions.Where(reception => reception.NodeNum == nodeNum)]);
        return repository;
    }

    public static MeshNodeStores Stores(
        Mock<IMeshNodeRepository> nodes,
        Mock<IMeshGatewayRepository>? gateways = null,
        Mock<INodeReceptionRepository>? receptions = null,
        Mock<INodeRegistrationRepository>? registrations = null) =>
        new(nodes.Object, (receptions ?? ReceptionsOf()).Object, (gateways ?? GatewaysWith()).Object, (registrations ?? Registrations()).Object);

    /// <summary>Registrations repository with no registrations (unless set up otherwise by the test).</summary>
    public static Mock<INodeRegistrationRepository> Registrations(NodeRegistration? registration = null)
    {
        var registrations = new Mock<INodeRegistrationRepository>();
        registrations.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(registration);
        registrations.Setup(repo => repo.GetActiveForNodeAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration is { IsActive: true } ? registration : null);
        registrations.Setup(repo => repo.GetVerifiedNodeNumsAsync(It.IsAny<IReadOnlyCollection<uint>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<uint>());
        return registrations;
    }

    /// <summary>A claimed registration of the hiker node by <see cref="UserName"/> with code 123456.</summary>
    public static NodeRegistration ClaimedRegistration(string userId = UserName, DateTimeOffset? at = null) =>
        NodeRegistration.Claim(HikerNodeNum, userId, userId, "Hiker", "HKR", GatewayPublicKey, null, "123456", at ?? Now);

    public static MeshMessage QueuedMessage(uint? to = HikerNodeNum, uint packetId = 4242) =>
        MeshMessage.QueueOutbound(0, "LongFast", to, "Are you OK?", MessageKind.Text, packetId, GatewayNodeNum, VirtualNodeNum, UserName, UserName, Now.AddSeconds(-30));

    public static Mock<IMeshMessageRepository> MessagesReturning(MeshMessage? message)
    {
        var messages = new Mock<IMeshMessageRepository>();
        messages.Setup(repo => repo.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(message);
        messages.Setup(repo => repo.GetOutboundByPacketIdAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>())).ReturnsAsync(message);
        messages.Setup(repo => repo.GetQueuedAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        return messages;
    }

    public static Mock<IMeshOutbox> Outbox(uint packetId = 777)
    {
        var outbox = new Mock<IMeshOutbox>();
        outbox.Setup(port => port.NewPacketId()).Returns(packetId);
        outbox.SetupGet(port => port.VirtualNodeNum).Returns(VirtualNodeNum);
        return outbox;
    }

    public static Mock<ICurrentUser> CurrentUser(string name = UserName)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(user => user.Name).Returns(name);
        currentUser.SetupGet(user => user.Id).Returns(name);
        return currentUser;
    }

    public static Mock<IPublisher> Publisher() => new();
}
