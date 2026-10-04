using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordGatewayStatus;
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
        var handler = new RecordNodeHeardHandler(nodes.Object, GatewaysReturning(Gateway(GatewayStatus.Online)).Object, Registrations().Object, FixedTime(), publisher.Object);

        // Act
        await handler.Handle(new RecordNodeHeardCommand(HikerNodeNum, Now, 5.0, -80, 1), CancellationToken.None);

        // Assert
        nodes.Verify(repo => repo.AddAsync(It.Is<MeshNode>(node => node.NodeNum == HikerNodeNum && node.LastHeardAt == Now), It.IsAny<CancellationToken>()), Times.Once);
        nodes.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(pub => pub.Publish(
            It.Is<NodeUpdatedNotification>(notification => notification.Node.NodeNum == HikerNodeNum && notification.Node.IsOnline),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RecordNodeHeard_KnownNode_UpdatesInsteadOfAdding()
    {
        // Arrange
        var node = KnownNode();
        var nodes = NodesReturning(node);
        var handler = new RecordNodeHeardHandler(nodes.Object, GatewaysReturning(null).Object, Registrations().Object, FixedTime(), Publisher().Object);

        // Act
        await handler.Handle(new RecordNodeHeardCommand(HikerNodeNum, Now, null, null, null), CancellationToken.None);

        // Assert
        nodes.Verify(repo => repo.UpdateAsync(node, It.IsAny<CancellationToken>()), Times.Once);
        nodes.Verify(repo => repo.AddAsync(It.IsAny<MeshNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RecordPosition_NewerFix_AddsHistoryRow()
    {
        // Arrange
        var nodes = NodesReturning(KnownNode());
        var handler = new RecordPositionHandler(nodes.Object, GatewaysReturning(null).Object, Registrations().Object, FixedTime(), Publisher().Object);

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
        var handler = new RecordPositionHandler(nodes.Object, GatewaysReturning(null).Object, Registrations().Object, FixedTime(), Publisher().Object);

        // Act
        await handler.Handle(new RecordPositionCommand(HikerNodeNum, new RadioPosition(51, 5, null, Now.AddHours(-1), 32), Now), CancellationToken.None);

        // Assert
        nodes.Verify(repo => repo.AddPositionAsync(It.IsAny<NodePosition>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RecordGatewayStatus_Unchanged_DoesNotSaveOrPublish()
    {
        // Arrange
        var gateways = GatewaysReturning(Gateway(GatewayStatus.Online));
        var publisher = Publisher();
        var handler = new RecordGatewayStatusHandler(gateways.Object, FixedTime(), publisher.Object);

        // Act
        await handler.Handle(new RecordGatewayStatusCommand(GatewayStatus.Online, "Tcp", null), CancellationToken.None);

        // Assert
        gateways.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        publisher.Verify(pub => pub.Publish(It.IsAny<GatewayStatusChangedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RecordGatewayStatus_FirstStart_RegistersGatewayAndPublishes()
    {
        // Arrange
        var gateways = GatewaysReturning(null);
        var publisher = Publisher();
        var handler = new RecordGatewayStatusHandler(gateways.Object, FixedTime(), publisher.Object);

        // Act
        await handler.Handle(new RecordGatewayStatusCommand(GatewayStatus.Connecting, "Simulated", null), CancellationToken.None);

        // Assert
        gateways.Verify(repo => repo.AddAsync(It.Is<MeshGateway>(gateway => gateway.Status == GatewayStatus.Connecting), It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(pub => pub.Publish(
            It.Is<GatewayStatusChangedNotification>(notification => notification.Gateway.Status == "Connecting" && notification.Gateway.Mode == "Simulated"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RequestPosition_UnknownNode_ThrowsKeyNotFoundAndQueuesNothing()
    {
        // Arrange
        var port = MeshGatewayPort();
        var handler = new RequestPositionHandler(NodesReturning(null).Object, GatewaysReturning(Gateway(GatewayStatus.Online)).Object, port.Object);

        // Act + Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await handler.Handle(new RequestPositionCommand(HikerNodeNum), CancellationToken.None));
        port.Verify(gateway => gateway.Enqueue(It.IsAny<MeshOutboundRequest>()), Times.Never);
    }

    [TestMethod]
    public async Task RequestPosition_GatewayOffline_ThrowsDomainException()
    {
        // Arrange
        var port = MeshGatewayPort();
        var handler = new RequestPositionHandler(NodesReturning(KnownNode()).Object, GatewaysReturning(Gateway(GatewayStatus.Offline)).Object, port.Object);

        // Act + Assert
        await Should.ThrowAsync<DomainException>(async () =>
            await handler.Handle(new RequestPositionCommand(HikerNodeNum), CancellationToken.None));
        port.Verify(gateway => gateway.Enqueue(It.IsAny<MeshOutboundRequest>()), Times.Never);
    }

    [TestMethod]
    public async Task RequestPosition_Online_QueuesPositionRequest()
    {
        // Arrange
        var port = MeshGatewayPort(packetId: 555);
        var handler = new RequestPositionHandler(NodesReturning(KnownNode()).Object, GatewaysReturning(Gateway(GatewayStatus.Online)).Object, port.Object);

        // Act
        await handler.Handle(new RequestPositionCommand(HikerNodeNum), CancellationToken.None);

        // Assert
        port.Verify(gateway => gateway.Enqueue(new PositionRequest(HikerNodeNum, 555)), Times.Once);
    }

    [TestMethod]
    public async Task RequestTraceroute_Online_SavesPendingAndQueuesSamePacketId()
    {
        // Arrange
        var traceroutes = new Mock<INodeTracerouteRepository>();
        var port = MeshGatewayPort(packetId: 4242);
        var handler = new RequestTracerouteHandler(
            NodesReturning(KnownNode()).Object,
            GatewaysReturning(Gateway(GatewayStatus.Online)).Object,
            traceroutes.Object,
            port.Object,
            CurrentUser().Object,
            FixedTime());

        // Act
        var result = await handler.Handle(new RequestTracerouteCommand(HikerNodeNum), CancellationToken.None);

        // Assert
        result.Status.ShouldBe("Pending");
        result.RequestedBy.ShouldBe(UserName);
        traceroutes.Verify(repo => repo.AddAsync(It.Is<NodeTraceroute>(route => route.PacketId == 4242), It.IsAny<CancellationToken>()), Times.Once);
        traceroutes.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        port.Verify(gateway => gateway.Enqueue(new TracerouteRequest(HikerNodeNum, 4242)), Times.Once);
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
}
