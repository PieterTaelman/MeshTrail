using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeHeard;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordPosition;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordTracerouteResult;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestPosition;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestTraceroute;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;
using Moq;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

[TestClass]
public sealed class MeshHandlerTests
{
    [TestMethod]
    public async Task RecordNodeHeard_NewNode_DiscoversSavesAndPublishes()
    {
        // Arrange
        var nodes = NodesReturning(null);
        var publisher = Publisher();
        var handler = new RecordNodeHeardHandler(Stores(nodes), FixedTime(), publisher.Object);

        // Act
        await handler.Handle(new RecordNodeHeardCommand(HikerNodeNum, GatewayNodeNum, Now, 5.0, -80, 1), CancellationToken.None);

        // Assert
        nodes.Verify(repo => repo.AddAsync(It.Is<MeshNode>(node => node.NodeNum == HikerNodeNum && node.LastHeardAt == Now), It.IsAny<CancellationToken>()), Times.Once);
        nodes.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(pub => pub.Publish(
            It.Is<NodeUpdatedNotification>(notification => notification.Node.NodeNum == HikerNodeNum && notification.Node.IsOnline),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RecordNodeHeard_Always_RecordsTheReceptionOfThatGateway()
    {
        // Arrange
        var receptions = ReceptionsOf();
        var handler = new RecordNodeHeardHandler(Stores(NodesReturning(KnownNode()), receptions: receptions), FixedTime(), Publisher().Object);

        // Act
        await handler.Handle(new RecordNodeHeardCommand(HikerNodeNum, OtherGatewayNodeNum, Now, 5.0, -80, 2), CancellationToken.None);

        // Assert
        receptions.Verify(repo => repo.RecordAsync(HikerNodeNum, OtherGatewayNodeNum, Now, 5.0, -80, 2, Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RecordNodeHeard_GatewayNode_IsPublishedAsGateway()
    {
        // Arrange
        var publisher = Publisher();
        var handler = new RecordNodeHeardHandler(
            Stores(NodesReturning(KnownNode(GatewayNodeNum)), GatewaysWith(BoundGateway())), FixedTime(), publisher.Object);

        // Act
        await handler.Handle(new RecordNodeHeardCommand(GatewayNodeNum, GatewayNodeNum, Now, null, null, 0), CancellationToken.None);

        // Assert
        publisher.Verify(pub => pub.Publish(It.Is<NodeUpdatedNotification>(notification => notification.Node.IsGateway), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RecordPosition_NewerFix_AddsHistoryRow()
    {
        // Arrange
        var nodes = NodesReturning(KnownNode());
        var handler = new RecordPositionHandler(Stores(nodes), FixedTime(), Publisher().Object);

        // Act
        await handler.Handle(new RecordPositionCommand(HikerNodeNum, new RadioPosition(50.1, 4.2, 100, null, 32), Now), CancellationToken.None);

        // Assert
        nodes.Verify(repo => repo.AddPositionAsync(
            It.Is<NodePosition>(position => position.NodeNum == HikerNodeNum && position.Position.Time == Now),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RecordPosition_OlderFix_DoesNotAddHistoryRow()
    {
        // Arrange
        var node = KnownNode();
        node.RecordPosition(Fix(time: Now), Now);
        var nodes = NodesReturning(node);
        var handler = new RecordPositionHandler(Stores(nodes), FixedTime(), Publisher().Object);

        // Act
        await handler.Handle(new RecordPositionCommand(HikerNodeNum, new RadioPosition(51, 5, null, Now.AddHours(-1), 32), Now), CancellationToken.None);

        // Assert
        nodes.Verify(repo => repo.AddPositionAsync(It.IsAny<NodePosition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RequestPosition_UnknownNode_ThrowsKeyNotFoundAndQueuesNothing()
    {
        // Arrange
        var outbox = Outbox();
        var handler = PositionHandler(NodesReturning(null), ReceptionsOf(Heard()), GatewaysWith(BoundGateway()), outbox);

        // Act + Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await handler.Handle(new RequestPositionCommand(HikerNodeNum), CancellationToken.None));
        outbox.Verify(port => port.Enqueue(It.IsAny<MeshOutboundRequest>()), Times.Never);
    }

    [TestMethod]
    public async Task RequestPosition_NoGatewayHeardTheNode_ThrowsDomainException()
    {
        // Arrange
        var outbox = Outbox();
        var handler = PositionHandler(NodesReturning(KnownNode()), ReceptionsOf(), GatewaysWith(BoundGateway()), outbox);

        // Act + Assert
        var exception = await Should.ThrowAsync<DomainException>(async () =>
            await handler.Handle(new RequestPositionCommand(HikerNodeNum), CancellationToken.None));
        exception.Message.ShouldStartWith("No gateway can reach node");
        outbox.Verify(port => port.Enqueue(It.IsAny<MeshOutboundRequest>()), Times.Never);
    }

    [TestMethod]
    public async Task RequestPosition_OnlyAnOfflineGatewayHeardIt_ThrowsDomainException()
    {
        // Arrange
        var handler = PositionHandler(NodesReturning(KnownNode()), ReceptionsOf(Heard()), GatewaysWith(BoundGateway(online: false)), Outbox());

        // Act + Assert
        await Should.ThrowAsync<DomainException>(async () =>
            await handler.Handle(new RequestPositionCommand(HikerNodeNum), CancellationToken.None));
    }

    [TestMethod]
    public async Task RequestPosition_TwoGatewaysHeardIt_QueuesViaTheBestOne()
    {
        // Arrange
        var outbox = Outbox(packetId: 555);
        var receptions = ReceptionsOf(Heard(GatewayNodeNum, hops: 2), Heard(OtherGatewayNodeNum, hops: 0));
        var gateways = GatewaysWith(BoundGateway(), BoundGateway(OtherGatewayNodeNum, login: "gw-other00001"));
        var handler = PositionHandler(NodesReturning(KnownNode()), receptions, gateways, outbox);

        // Act
        await handler.Handle(new RequestPositionCommand(HikerNodeNum), CancellationToken.None);

        // Assert
        outbox.Verify(port => port.Enqueue(It.Is<PositionRequest>(request =>
            request.NodeNum == HikerNodeNum && request.PacketId == 555 && request.Via.GatewayNodeNum == OtherGatewayNodeNum && request.Via.MqttUserName == "gw-other00001")), Times.Once);
    }

    [TestMethod]
    public async Task RequestTraceroute_Reachable_SavesPendingAndQueuesSamePacketId()
    {
        // Arrange
        var traceroutes = new Mock<INodeTracerouteRepository>();
        var outbox = Outbox(packetId: 4242);
        var handler = new RequestTracerouteHandler(
            NodesReturning(KnownNode()).Object,
            ReceptionsOf(Heard()).Object,
            GatewaysWith(BoundGateway()).Object,
            traceroutes.Object,
            outbox.Object,
            CurrentUser().Object,
            FixedTime());

        // Act
        var result = await handler.Handle(new RequestTracerouteCommand(HikerNodeNum), CancellationToken.None);

        // Assert
        result.Status.ShouldBe("Pending");
        result.RequestedBy.ShouldBe(UserName);
        traceroutes.Verify(repo => repo.AddAsync(It.Is<NodeTraceroute>(route => route.PacketId == 4242), It.IsAny<CancellationToken>()), Times.Once);
        traceroutes.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        outbox.Verify(port => port.Enqueue(new TracerouteRequest(Route(), HikerNodeNum, 4242)), Times.Once);
    }

    [TestMethod]
    public async Task RecordTracerouteResult_UnknownPacket_IsIgnored()
    {
        // Arrange
        var traceroutes = new Mock<INodeTracerouteRepository>();
        var publisher = Publisher();
        var handler = new RecordTracerouteResultHandler(traceroutes.Object, FixedTime(), publisher.Object);

        // Act
        await handler.Handle(new RecordTracerouteResultCommand(HikerNodeNum, 1, [], [], [], []), CancellationToken.None);

        // Assert
        traceroutes.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        publisher.Verify(pub => pub.Publish(It.IsAny<TracerouteCompletedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RecordTracerouteResult_MatchingPacket_CompletesAndPublishes()
    {
        // Arrange
        var traceroute = NodeTraceroute.Request(HikerNodeNum, 42, UserName, Now.AddSeconds(-10));
        var traceroutes = new Mock<INodeTracerouteRepository>();
        traceroutes.Setup(repo => repo.GetByPacketIdAsync(HikerNodeNum, 42, It.IsAny<CancellationToken>())).ReturnsAsync(traceroute);
        var publisher = Publisher();
        var handler = new RecordTracerouteResultHandler(traceroutes.Object, FixedTime(), publisher.Object);

        // Act
        await handler.Handle(new RecordTracerouteResultCommand(HikerNodeNum, 42, [7u], [6.0], [], []), CancellationToken.None);

        // Assert
        traceroute.Status.ShouldBe(TracerouteStatus.Completed);
        traceroutes.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(pub => pub.Publish(
            It.Is<TracerouteCompletedNotification>(notification => notification.Traceroute.RouteTowards.Single().NodeNum == 7u),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static RequestPositionHandler PositionHandler(
        Mock<IMeshNodeRepository> nodes, Mock<INodeReceptionRepository> receptions, Mock<IMeshGatewayRepository> gateways, Mock<IMeshOutbox> outbox) =>
        new(nodes.Object, receptions.Object, gateways.Object, outbox.Object, FixedTime());
}
