using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.FailTimedOutMessages;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.ReceiveTextMessage;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordRoutingResult;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RegisterFromContactUrl;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.SendMessage;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.VerifyRegistration;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;
using Moq;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

[TestClass]
public sealed class MessagingHandlerTests
{
    // ---- delivery reports

    [TestMethod]
    public async Task RecordRouting_DirectMessageAckFromDestination_IsAcked()
    {
        // Arrange
        var message = QueuedMessage();
        message.MarkSent(Now);
        var messages = MessagesReturning(message);
        var publisher = Publisher();

        // Act
        await RoutingHandler(messages, publisher).Handle(new RecordRoutingResultCommand(HikerNodeNum, message.PacketId, "None"), CancellationToken.None);

        // Assert
        message.Status.ShouldBe(MessageStatus.Acked);
        messages.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(pub => pub.Publish(It.Is<MessageStatusChangedNotification>(n => n.Message.Status == "Acked"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task RecordRouting_DirectMessageImplicitAckFromGateway_StaysSent()
    {
        // Arrange
        var message = QueuedMessage();
        message.MarkSent(Now);
        var messages = MessagesReturning(message);

        // Act
        await RoutingHandler(messages, Publisher()).Handle(new RecordRoutingResultCommand(GatewayNodeNum, message.PacketId, "None"), CancellationToken.None);

        // Assert
        message.Status.ShouldBe(MessageStatus.Sent);
        messages.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RecordRouting_BroadcastAckFromAnyone_IsAcked()
    {
        // Arrange
        var message = QueuedMessage(to: null);
        message.MarkSent(Now);

        // Act
        await RoutingHandler(MessagesReturning(message), Publisher()).Handle(new RecordRoutingResultCommand(GatewayNodeNum, message.PacketId, "None"), CancellationToken.None);

        // Assert
        message.Status.ShouldBe(MessageStatus.Acked);
    }

    [TestMethod]
    public async Task RecordRouting_Error_IsFailedWithReason()
    {
        // Arrange
        var message = QueuedMessage();
        message.MarkSent(Now);

        // Act
        await RoutingHandler(MessagesReturning(message), Publisher()).Handle(new RecordRoutingResultCommand(GatewayNodeNum, message.PacketId, "NoRoute"), CancellationToken.None);

        // Assert
        message.Status.ShouldBe(MessageStatus.Failed);
        message.FailureReason.ShouldBe("NoRoute");
    }

    [TestMethod]
    public async Task RecordRouting_AckBeforeOurSentBookkeeping_IsAcked()
    {
        // Arrange
        var message = QueuedMessage();

        // Act
        await RoutingHandler(MessagesReturning(message), Publisher()).Handle(new RecordRoutingResultCommand(HikerNodeNum, message.PacketId, "None"), CancellationToken.None);

        // Assert
        message.Status.ShouldBe(MessageStatus.Acked);
        message.SentAt.ShouldBe(Now);
    }

    [TestMethod]
    public async Task RecordRouting_UnknownPacket_IsIgnored()
    {
        // Arrange
        var messages = MessagesReturning(null);

        // Act
        await RoutingHandler(messages, Publisher()).Handle(new RecordRoutingResultCommand(HikerNodeNum, 1, "None"), CancellationToken.None);

        // Assert
        messages.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task FailTimedOut_SentWithoutReport_IsFailed()
    {
        // Arrange
        var message = QueuedMessage();
        message.MarkSent(Now.AddMinutes(-5));
        var messages = new Mock<IMeshMessageRepository>();
        messages.Setup(repo => repo.GetSentBeforeAsync(Now, It.IsAny<CancellationToken>())).ReturnsAsync([message]);
        messages.Setup(repo => repo.GetQueuedBeforeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        // Act
        var count = await new FailTimedOutMessagesHandler(messages.Object, Publisher().Object)
            .Handle(new FailTimedOutMessagesCommand(Now, Now.AddMinutes(-10)), CancellationToken.None);

        // Assert
        count.ShouldBe(1);
        message.Status.ShouldBe(MessageStatus.Failed);
        message.FailureReason.ShouldBe(FailTimedOutMessagesHandler.TimeoutReason);
    }

    [TestMethod]
    public async Task FailTimedOut_QueuedBehindOfflineGateway_IsFailed()
    {
        // Arrange
        var message = QueuedMessage();
        var messages = new Mock<IMeshMessageRepository>();
        messages.Setup(repo => repo.GetSentBeforeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        messages.Setup(repo => repo.GetQueuedBeforeAsync(Now, It.IsAny<CancellationToken>())).ReturnsAsync([message]);

        // Act
        await new FailTimedOutMessagesHandler(messages.Object, Publisher().Object).Handle(new FailTimedOutMessagesCommand(Now.AddMinutes(-2), Now), CancellationToken.None);

        // Assert
        message.Status.ShouldBe(MessageStatus.Failed);
        message.FailureReason.ShouldBe(FailTimedOutMessagesHandler.GatewayOfflineReason);
    }

    // ---- receiving

    [TestMethod]
    public async Task Receive_ChannelMessage_IsStoredButNotPushed()
    {
        // Arrange
        var messages = MessagesReturning(null);
        var publisher = Publisher();

        // Act
        await new ReceiveTextMessageHandler(messages.Object, TeamsWith().Object, publisher.Object).Handle(Text(to: uint.MaxValue), CancellationToken.None);

        // Assert
        messages.Verify(repo => repo.AddAsync(It.Is<MeshMessage>(m => m.ToNodeNum == null && m.ChannelName == "LongFast"), It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(pub => pub.Publish(It.IsAny<MessageReceivedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Receive_DirectMessageToUs_IsStoredAndPushed()
    {
        // Arrange
        var publisher = Publisher();

        // Act
        await new ReceiveTextMessageHandler(MessagesReturning(null).Object, TeamsWith().Object, publisher.Object).Handle(Text(to: VirtualNodeNum), CancellationToken.None);

        // Assert
        publisher.Verify(pub => pub.Publish(
            It.Is<MessageReceivedNotification>(n => n.Message.PeerNodeNum == HikerNodeNum && n.Message.GatewayNodeNum == GatewayNodeNum),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- sending

    [TestMethod]
    public async Task SendMessage_NoGatewayCanReachTheNode_ThrowsAndStoresNothing()
    {
        // Arrange
        var messages = MessagesReturning(null);
        var handler = SendHandler(messages, ReceptionsOf(), Outbox());

        // Act + Assert
        await Should.ThrowAsync<DomainException>(async () => await handler.Handle(new SendMessageCommand(HikerNodeNum, null, "hi"), CancellationToken.None));
        messages.Verify(repo => repo.AddAsync(It.IsAny<MeshMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SendMessage_Reachable_IsStoredThenQueuedOnTheBestGatewayFromOurVirtualNode()
    {
        // Arrange
        var messages = MessagesReturning(null);
        MeshMessage? stored = null;
        messages.Setup(repo => repo.AddAsync(It.IsAny<MeshMessage>(), It.IsAny<CancellationToken>())).Callback((MeshMessage m, CancellationToken _) => stored = m);
        var outbox = Outbox(packetId: 99);
        var handler = SendHandler(messages, ReceptionsOf(Heard()), outbox);

        // Act
        var result = await handler.Handle(new SendMessageCommand(HikerNodeNum, null, "hi"), CancellationToken.None);

        // Assert
        result.Status.ShouldBe("Queued");
        result.GatewayNodeNum.ShouldBe(GatewayNodeNum);
        stored.ShouldNotBeNull().FromNodeNum.ShouldBe(VirtualNodeNum);
        messages.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        outbox.Verify(port => port.Enqueue(It.Is<TextMessageRequest>(r =>
            r.NodeNum == HikerNodeNum && r.PacketId == 99 && r.MessageId == result.Id && r.Via == Route(GatewayNodeNum))), Times.Once);
    }

    // ---- registration

    [TestMethod]
    public async Task Register_NodeNeverHeard_Throws()
    {
        // Arrange
        var handler = RegisterHandler(Registrations(), MessagesReturning(null), Outbox(), NodesReturning(null));

        // Act + Assert
        var exception = await Should.ThrowAsync<DomainException>(async () =>
            await handler.Handle(new RegisterFromContactUrlCommand(GatewayContactUrl), CancellationToken.None));
        exception.Message.ShouldContain("has not been heard by any gateway yet");
    }

    [TestMethod]
    public async Task Register_OwnEarlierClaim_IsReplaced()
    {
        // Arrange
        var earlier = ClaimedRegistration(at: Now.AddMinutes(-2));
        var registrations = Registrations(earlier);
        var handler = RegisterHandler(registrations, MessagesReturning(null), Outbox());

        // Act
        var result = await handler.Handle(new RegisterFromContactUrlCommand(GatewayContactUrl), CancellationToken.None);

        // Assert
        earlier.Status.ShouldBe(RegistrationStatus.Revoked);
        result.Status.ShouldBe("Claimed");
        registrations.Verify(repo => repo.AddAsync(It.IsAny<NodeRegistration>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Register_SomeoneElsesFreshClaim_Throws()
    {
        // Arrange
        var registrations = Registrations(ClaimedRegistration(userId: "someone-else", at: Now.AddMinutes(-2)));
        var handler = RegisterHandler(registrations, MessagesReturning(null), Outbox());

        // Act + Assert
        var exception = await Should.ThrowAsync<DomainException>(async () =>
            await handler.Handle(new RegisterFromContactUrlCommand(GatewayContactUrl), CancellationToken.None));
        exception.Message.ShouldContain("Try again in 13 minutes");
    }

    [TestMethod]
    public async Task Register_SomeoneElsesExpiredClaim_IsReplaced()
    {
        // Arrange
        var expired = ClaimedRegistration(userId: "someone-else", at: Now.AddMinutes(-20));
        var handler = RegisterHandler(Registrations(expired), MessagesReturning(null), Outbox());

        // Act
        await handler.Handle(new RegisterFromContactUrlCommand(GatewayContactUrl), CancellationToken.None);

        // Assert
        expired.Status.ShouldBe(RegistrationStatus.Revoked);
        expired.RevokedReason.ShouldBe("Code expired.");
    }

    [TestMethod]
    public async Task Register_Valid_QueuesHiddenVerificationMessageWithCode()
    {
        // Arrange
        var messages = MessagesReturning(null);
        var outbox = Outbox();
        var handler = RegisterHandler(Registrations(), messages, outbox);

        // Act
        var result = await handler.Handle(new RegisterFromContactUrlCommand(GatewayContactUrl), CancellationToken.None);

        // Assert
        result.VerificationMessageStatus.ShouldBe("Queued");
        messages.Verify(repo => repo.AddAsync(It.Is<MeshMessage>(m => m.Kind == MessageKind.Verification && m.Text.Contains("123456")), It.IsAny<CancellationToken>()), Times.Once);
        outbox.Verify(port => port.Enqueue(It.Is<TextMessageRequest>(r => r.NodeNum == GatewayNodeNum && r.Text.Contains("123456"))), Times.Once);
    }

    [TestMethod]
    public async Task Verify_WrongCode_SavesAttemptThenThrows()
    {
        // Arrange
        var registration = ClaimedRegistration();
        var registrations = Registrations(registration);
        var handler = VerifyHandler(registrations, Outbox());

        // Act + Assert
        var exception = await Should.ThrowAsync<DomainException>(async () =>
            await handler.Handle(new VerifyRegistrationCommand(registration.Id, "999999"), CancellationToken.None));
        exception.Message.ShouldBe("Wrong code. 4 attempts left.");
        registrations.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Verify_RightCode_SendsContactToTcpGatewaysOnly()
    {
        // Arrange
        var registration = ClaimedRegistration();
        var outbox = Outbox();
        var tcp = MeshGateway.Local(GatewayTransport.Tcp, OtherGatewayNodeNum, Now);
        var handler = VerifyHandler(Registrations(registration), outbox, GatewaysWith(BoundGateway(), tcp));

        // Act
        var result = await handler.Handle(new VerifyRegistrationCommand(registration.Id, "123456"), CancellationToken.None);

        // Assert
        result.Status.ShouldBe("Verified");
        outbox.Verify(port => port.Enqueue(It.Is<AddContactRequest>(r =>
            r.NodeNum == HikerNodeNum && r.Via.GatewayNodeNum == OtherGatewayNodeNum && r.PublicKey.SequenceEqual(GatewayPublicKey))), Times.Once);
        outbox.Verify(port => port.Enqueue(It.Is<AddContactRequest>(r => r.Via.GatewayNodeNum == GatewayNodeNum)), Times.Never);
    }

    [TestMethod]
    public async Task Verify_SomeoneElsesRegistration_ThrowsKeyNotFound()
    {
        // Arrange
        var registration = ClaimedRegistration(userId: "someone-else");
        var handler = VerifyHandler(Registrations(registration), Outbox());

        // Act + Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await handler.Handle(new VerifyRegistrationCommand(registration.Id, "123456"), CancellationToken.None));
    }

    // ---- builders

    private static ReceiveTextMessageCommand Text(uint to) =>
        new(HikerNodeNum, to, 0, "LongFast", GatewayNodeNum, "hi", 77, 5, -80, 1, Now);

    private static RecordRoutingResultHandler RoutingHandler(Mock<IMeshMessageRepository> messages, Mock<Mediator.IPublisher> publisher) =>
        new(messages.Object, FixedTime(), publisher.Object);

    private static SendMessageHandler SendHandler(Mock<IMeshMessageRepository> messages, Mock<INodeReceptionRepository> receptions, Mock<IMeshOutbox> outbox) =>
        new(
            NodesReturning(KnownNode()).Object,
            receptions.Object,
            GatewaysWith(BoundGateway()).Object,
            TeamsWith().Object,
            messages.Object,
            outbox.Object,
            CurrentUser().Object,
            FixedTime(),
            Publisher().Object);

    private static RegisterFromContactUrlHandler RegisterHandler(
        Mock<INodeRegistrationRepository> registrations, Mock<IMeshMessageRepository> messages, Mock<IMeshOutbox> outbox, Mock<IMeshNodeRepository>? nodes = null)
    {
        var parser = new Mock<IContactUrlParser>();
        var contact = new ParsedContact(GatewayNodeNum, "Node 2109", "2109", "M5StackC6L", "Client", GatewayPublicKey);
        parser.Setup(p => p.TryParse(It.IsAny<string>(), out contact, out It.Ref<string>.IsAny)).Returns(true);
        var codes = new Mock<IVerificationCodeGenerator>();
        codes.Setup(generator => generator.NewCode()).Returns("123456");

        // The registered node is heard by another gateway (a node cannot be routed through itself in this example).
        return new RegisterFromContactUrlHandler(
            parser.Object,
            (nodes ?? NodesReturning(KnownNode(GatewayNodeNum))).Object,
            ReceptionsOf(Heard(OtherGatewayNodeNum, nodeNum: GatewayNodeNum)).Object,
            GatewaysWith(BoundGateway(OtherGatewayNodeNum)).Object,
            registrations.Object,
            messages.Object,
            outbox.Object,
            codes.Object,
            CurrentUser().Object,
            FixedTime(),
            Publisher().Object);
    }

    private static VerifyRegistrationHandler VerifyHandler(
        Mock<INodeRegistrationRepository> registrations, Mock<IMeshOutbox> outbox, Mock<IMeshGatewayRepository>? gateways = null) =>
        new(
            registrations.Object,
            Stores(NodesReturning(KnownNode()), gateways ?? GatewaysWith(BoundGateway()), registrations: registrations),
            MessagesReturning(null).Object,
            outbox.Object,
            CurrentUser().Object,
            FixedTime(),
            Publisher().Object);
}
