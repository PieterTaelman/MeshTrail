using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.FailTimedOutMessages;
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
        message.MarkSent(GatewayNodeNum, Now);
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
        message.MarkSent(GatewayNodeNum, Now);
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
        message.MarkSent(GatewayNodeNum, Now);

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
        message.MarkSent(GatewayNodeNum, Now);

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
        message.MarkSent(GatewayNodeNum, Now.AddMinutes(-5));
        var messages = new Mock<IMeshMessageRepository>();
        messages.Setup(repo => repo.GetSentBeforeAsync(Now, It.IsAny<CancellationToken>())).ReturnsAsync([message]);

        // Act
        var count = await new FailTimedOutMessagesHandler(messages.Object, Publisher().Object).Handle(new FailTimedOutMessagesCommand(Now), CancellationToken.None);

        // Assert
        count.ShouldBe(1);
        message.Status.ShouldBe(MessageStatus.Failed);
        message.FailureReason.ShouldBe(FailTimedOutMessagesHandler.TimeoutReason);
    }

    // ---- sending

    [TestMethod]
    public async Task SendMessage_GatewayOffline_ThrowsAndStoresNothing()
    {
        // Arrange
        var messages = MessagesReturning(null);
        var handler = SendHandler(messages, Gateway(GatewayStatus.Offline), MeshGatewayPort());

        // Act + Assert
        await Should.ThrowAsync<DomainException>(async () => await handler.Handle(new SendMessageCommand(0, null, "hi"), CancellationToken.None));
        messages.Verify(repo => repo.AddAsync(It.IsAny<MeshMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SendMessage_DirectMessage_IsStoredThenHandedToTheGateway()
    {
        // Arrange
        var messages = MessagesReturning(null);
        var port = MeshGatewayPort(packetId: 99);
        var handler = SendHandler(messages, Gateway(GatewayStatus.Online), port);

        // Act
        var result = await handler.Handle(new SendMessageCommand(3, HikerNodeNum, "hi"), CancellationToken.None);

        // Assert
        result.Status.ShouldBe("Queued");
        result.ChannelIndex.ShouldBe(0);
        messages.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        port.Verify(gateway => gateway.Enqueue(It.Is<TextMessageRequest>(r => r.NodeNum == HikerNodeNum && r.PacketId == 99 && r.MessageId == result.Id)), Times.Once);
    }

    // ---- registration

    [TestMethod]
    public async Task Register_OwnEarlierClaim_IsReplaced()
    {
        // Arrange
        var earlier = ClaimedRegistration(at: Now.AddMinutes(-2));
        var registrations = Registrations(earlier);
        var handler = RegisterHandler(registrations, MessagesReturning(null), MeshGatewayPort());

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
        var handler = RegisterHandler(registrations, MessagesReturning(null), MeshGatewayPort());

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
        var handler = RegisterHandler(Registrations(expired), MessagesReturning(null), MeshGatewayPort());

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
        var port = MeshGatewayPort();
        var handler = RegisterHandler(Registrations(), messages, port);

        // Act
        var result = await handler.Handle(new RegisterFromContactUrlCommand(GatewayContactUrl), CancellationToken.None);

        // Assert
        result.VerificationMessageStatus.ShouldBe("Queued");
        messages.Verify(repo => repo.AddAsync(It.Is<MeshMessage>(m => m.Kind == MessageKind.Verification && m.Text.Contains("123456")), It.IsAny<CancellationToken>()), Times.Once);
        port.Verify(gateway => gateway.Enqueue(It.Is<TextMessageRequest>(r => r.NodeNum == GatewayNodeNum && r.Text.Contains("123456"))), Times.Once);
    }

    [TestMethod]
    public async Task Verify_WrongCode_SavesAttemptThenThrows()
    {
        // Arrange
        var registration = ClaimedRegistration();
        var registrations = Registrations(registration);
        var handler = VerifyHandler(registrations, MeshGatewayPort());

        // Act + Assert
        var exception = await Should.ThrowAsync<DomainException>(async () =>
            await handler.Handle(new VerifyRegistrationCommand(registration.Id, "999999"), CancellationToken.None));
        exception.Message.ShouldBe("Wrong code. 4 attempts left.");
        registrations.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Verify_RightCode_SendsContactToGateway()
    {
        // Arrange
        var registration = ClaimedRegistration();
        var port = MeshGatewayPort();
        var handler = VerifyHandler(Registrations(registration), port);

        // Act
        var result = await handler.Handle(new VerifyRegistrationCommand(registration.Id, "123456"), CancellationToken.None);

        // Assert
        result.Status.ShouldBe("Verified");
        port.Verify(gateway => gateway.Enqueue(It.Is<AddContactRequest>(r => r.NodeNum == HikerNodeNum && r.PublicKey.SequenceEqual(GatewayPublicKey))), Times.Once);
    }

    [TestMethod]
    public async Task Verify_SomeoneElsesRegistration_ThrowsKeyNotFound()
    {
        // Arrange
        var registration = ClaimedRegistration(userId: "someone-else");
        var handler = VerifyHandler(Registrations(registration), MeshGatewayPort());

        // Act + Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await handler.Handle(new VerifyRegistrationCommand(registration.Id, "123456"), CancellationToken.None));
    }

    // ---- builders

    private static RecordRoutingResultHandler RoutingHandler(Mock<IMeshMessageRepository> messages, Mock<Mediator.IPublisher> publisher) =>
        new(messages.Object, GatewaysReturning(Gateway(GatewayStatus.Online)).Object, FixedTime(), publisher.Object);

    private static SendMessageHandler SendHandler(Mock<IMeshMessageRepository> messages, MeshGateway gateway, Mock<IMeshGateway> port) =>
        new(NodesReturning(KnownNode()).Object, GatewaysReturning(gateway).Object, messages.Object, port.Object, CurrentUser().Object, FixedTime(), Publisher().Object);

    private static RegisterFromContactUrlHandler RegisterHandler(
        Mock<INodeRegistrationRepository> registrations, Mock<IMeshMessageRepository> messages, Mock<IMeshGateway> port)
    {
        var parser = new Mock<IContactUrlParser>();
        var contact = new ParsedContact(GatewayNodeNum, "Node 2109", "2109", "M5StackC6L", "Client", GatewayPublicKey);
        parser.Setup(p => p.TryParse(It.IsAny<string>(), out contact, out It.Ref<string>.IsAny)).Returns(true);
        var codes = new Mock<IVerificationCodeGenerator>();
        codes.Setup(generator => generator.NewCode()).Returns("123456");

        return new RegisterFromContactUrlHandler(
            parser.Object,
            NodesReturning(KnownNode(GatewayNodeNum)).Object,
            GatewaysReturning(Gateway(GatewayStatus.Online)).Object,
            registrations.Object,
            messages.Object,
            port.Object,
            codes.Object,
            CurrentUser().Object,
            FixedTime(),
            Publisher().Object);
    }

    private static VerifyRegistrationHandler VerifyHandler(Mock<INodeRegistrationRepository> registrations, Mock<IMeshGateway> port)
    {
        var user = CurrentUser();
        user.SetupGet(u => u.Id).Returns(UserName);
        return new VerifyRegistrationHandler(
            registrations.Object,
            NodesReturning(KnownNode()).Object,
            GatewaysReturning(Gateway(GatewayStatus.Online)).Object,
            MessagesReturning(null).Object,
            port.Object,
            user.Object,
            FixedTime(),
            Publisher().Object);
    }
}
