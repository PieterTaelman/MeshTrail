using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Gateways.Commands.AddGateway;
using Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayConnections;
using Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayUplink;
using Meshtrail.Core.Application.UseCases.Gateways.Commands.RevokeGateway;
using Meshtrail.Core.Application.UseCases.Gateways.Events;
using Meshtrail.Core.Application.UseCases.Gateways.Queries.AuthenticateGateway;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;
using Moq;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

[TestClass]
public sealed class GatewayHandlerTests
{
    [TestMethod]
    public async Task AddGateway_Valid_StoresPendingGatewayAndReturnsThePasswordOnce()
    {
        // Arrange
        var gateways = GatewaysWith();
        MeshGateway? stored = null;
        gateways.Setup(repo => repo.AddAsync(It.IsAny<MeshGateway>(), It.IsAny<CancellationToken>()))
            .Callback((MeshGateway gateway, CancellationToken _) => stored = gateway);

        // Act
        var result = await AddHandler(gateways).Handle(new AddGatewayCommand(), CancellationToken.None);

        // Assert
        result.UserName.ShouldBe(GatewayLogin);
        result.Password.ShouldBe(GatewayPassword);
        result.Gateway.Status.ShouldBe("Pending");
        result.Gateway.IsMine.ShouldBeTrue();
        result.Setup.Root.ShouldBe("msh/EU_868");
        stored.ShouldNotBeNull().PasswordMatches(GatewayPassword).ShouldBeTrue();
        stored.OwnerUserId.ShouldBe(UserName);
    }

    [TestMethod]
    public async Task AddGateway_LimitReached_Throws()
    {
        // Arrange
        var gateways = GatewaysWith();
        gateways.Setup(repo => repo.CountActiveForOwnerAsync(UserName, It.IsAny<CancellationToken>())).ReturnsAsync(MeshGateway.MaxPerOwner);

        // Act + Assert
        await Should.ThrowAsync<DomainException>(async () => await AddHandler(gateways).Handle(new AddGatewayCommand(), CancellationToken.None));
        gateways.Verify(repo => repo.AddAsync(It.IsAny<MeshGateway>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RevokeGateway_SomeoneElses_ThrowsKeyNotFound()
    {
        // Arrange
        var gateway = PendingGateway(owner: "someone-else");
        var handler = new RevokeGatewayHandler(GatewaysWith(gateway).Object, NodesReturning(null).Object, CurrentUser().Object, FixedTime(), Publisher().Object);

        // Act + Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () => await handler.Handle(new RevokeGatewayCommand(gateway.Id), CancellationToken.None));
        gateway.Status.ShouldBe(GatewayStatus.Pending);
    }

    [TestMethod]
    public async Task RevokeGateway_Mine_IsRevokedAndPublished()
    {
        // Arrange
        var gateway = BoundGateway();
        var publisher = Publisher();
        var handler = new RevokeGatewayHandler(GatewaysWith(gateway).Object, NodesReturning(null).Object, CurrentUser().Object, FixedTime(), publisher.Object);

        // Act
        await handler.Handle(new RevokeGatewayCommand(gateway.Id), CancellationToken.None);

        // Assert
        gateway.Status.ShouldBe(GatewayStatus.Revoked);
        publisher.Verify(pub => pub.Publish(It.Is<GatewayStatusChangedNotification>(n => n.Gateway.Status == "Revoked"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Uplink_FirstWithPendingLogin_BindsTheNodeAndAccepts()
    {
        // Arrange
        var gateway = PendingGateway();
        var gateways = GatewaysWith(gateway);

        // Act
        var accepted = await UplinkHandler(gateways).Handle(Uplink(GatewayLogin, GatewayNodeNum), CancellationToken.None);

        // Assert
        accepted.ShouldBeTrue();
        gateway.NodeNum.ShouldBe(GatewayNodeNum);
        gateway.Status.ShouldBe(GatewayStatus.Online);
        gateway.DownlinkChannel.ShouldBe("LongFast");
        gateways.Verify(repo => repo.RecordChannelAsync(gateway.Id, "LongFast", Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Uplink_LoginOfAnotherNode_IsRejected()
    {
        // Arrange
        var gateway = BoundGateway(GatewayNodeNum);

        // Act
        var accepted = await UplinkHandler(GatewaysWith(gateway)).Handle(Uplink(GatewayLogin, OtherGatewayNodeNum), CancellationToken.None);

        // Assert
        accepted.ShouldBeFalse();
        gateway.NodeNum.ShouldBe(GatewayNodeNum);
    }

    [TestMethod]
    public async Task Uplink_UnknownLogin_IsRejected()
    {
        // Act
        var accepted = await UplinkHandler(GatewaysWith()).Handle(Uplink("gw-nobody", GatewayNodeNum), CancellationToken.None);

        // Assert
        accepted.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Uplink_UnknownLoginInDevelopment_BecomesOwnerlessGateway()
    {
        // Arrange
        var gateways = GatewaysWith();

        // Act
        var accepted = await UplinkHandler(gateways, acceptUnregistered: true).Handle(Uplink("anything", GatewayNodeNum), CancellationToken.None);

        // Assert
        accepted.ShouldBeTrue();
        gateways.Verify(repo => repo.AddAsync(
            It.Is<MeshGateway>(gateway => gateway.NodeNum == GatewayNodeNum && gateway.OwnerUserId == null && gateway.MqttUserName == "anything"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Uplink_NodeIsAlreadyAnotherUsersGateway_IsRejectedWithError()
    {
        // Arrange
        var theirs = PendingGateway(owner: "someone-else", login: "gw-theirs0001");
        theirs.Bind(GatewayNodeNum, Now.AddHours(-1));
        var mine = PendingGateway();
        var handler = UplinkHandler(GatewaysWith(theirs, mine));

        // Act
        var accepted = await handler.Handle(Uplink(GatewayLogin, GatewayNodeNum), CancellationToken.None);

        // Assert
        accepted.ShouldBeFalse();
        mine.NodeNum.ShouldBeNull();
        mine.LastError.ShouldBe(RecordGatewayUplinkHandler.NodeTakenError);
    }

    [TestMethod]
    public async Task Uplink_GatewayComesBackOnline_RequeuesItsWaitingMessages()
    {
        // Arrange
        var gateway = BoundGateway(online: false);
        var messages = MessagesReturning(null);
        messages.Setup(repo => repo.GetQueuedAsync(GatewayNodeNum, It.IsAny<CancellationToken>())).ReturnsAsync([QueuedMessage()]);
        var outbox = Outbox();

        // Act
        await UplinkHandler(GatewaysWith(gateway), messages: messages, outbox: outbox).Handle(Uplink(GatewayLogin, GatewayNodeNum), CancellationToken.None);

        // Assert
        outbox.Verify(port => port.Enqueue(It.Is<TextMessageRequest>(request => request.Via.GatewayNodeNum == GatewayNodeNum)), Times.Once);
    }

    [TestMethod]
    public async Task Connections_LoginMissing_GoesOffline()
    {
        // Arrange
        var connected = BoundGateway(GatewayNodeNum, login: "gw-connected1");
        var gone = BoundGateway(OtherGatewayNodeNum, login: "gw-gone000001");
        var gateways = GatewaysWith(connected, gone);
        var handler = new RecordGatewayConnectionsHandler(
            gateways.Object, NodesReturning(null).Object, MessagesReturning(null).Object, Outbox().Object, FixedTime(), Publisher().Object);

        // Act
        await handler.Handle(new RecordGatewayConnectionsCommand("local", ["gw-connected1"]), CancellationToken.None);

        // Assert
        connected.Status.ShouldBe(GatewayStatus.Online);
        gone.Status.ShouldBe(GatewayStatus.Offline);
        gone.LastError.ShouldBe(RecordGatewayConnectionsHandler.NotConnectedError);
    }

    [TestMethod]
    [DataRow(GatewayPassword, true)]
    [DataRow("wrong", false)]
    public async Task Authenticate_ChecksLoginAndPassword(string password, bool expected)
    {
        // Arrange
        var handler = new AuthenticateGatewayHandler(GatewaysWith(PendingGateway()).Object);

        // Act + Assert
        (await handler.Handle(new AuthenticateGatewayQuery(GatewayLogin, password), CancellationToken.None)).ShouldBe(expected);
    }

    // ---- builders

    private static RecordGatewayUplinkCommand Uplink(string login, uint gatewayNodeNum) =>
        new(GatewayTransport.Mqtt, gatewayNodeNum, login, "local", "msh/EU_868", "LongFast", Now);

    private static AddGatewayHandler AddHandler(Mock<IMeshGatewayRepository> gateways)
    {
        var credentials = new Mock<IGatewayCredentialGenerator>();
        credentials.Setup(generator => generator.NewCredentials()).Returns(new GatewayCredentials(GatewayLogin, GatewayPassword));
        return new AddGatewayHandler(gateways.Object, credentials.Object, Setup(false).Object, CurrentUser().Object, FixedTime(), Publisher().Object);
    }

    private static RecordGatewayUplinkHandler UplinkHandler(
        Mock<IMeshGatewayRepository> gateways,
        bool acceptUnregistered = false,
        Mock<IMeshMessageRepository>? messages = null,
        Mock<IMeshOutbox>? outbox = null) =>
        new(
            gateways.Object,
            NodesReturning(null).Object,
            (messages ?? MessagesReturning(null)).Object,
            (outbox ?? Outbox()).Object,
            Setup(acceptUnregistered).Object,
            FixedTime(),
            Publisher().Object);

    private static Mock<IGatewaySetup> Setup(bool acceptUnregistered)
    {
        var setup = new Mock<IGatewaySetup>();
        setup.SetupGet(s => s.Broker).Returns("local");
        setup.SetupGet(s => s.Setup).Returns(new MqttSetupDto(null, 1883, false, "msh/EU_868"));
        setup.SetupGet(s => s.AcceptUnregisteredGateways).Returns(acceptUnregistered);
        return setup;
    }
}
