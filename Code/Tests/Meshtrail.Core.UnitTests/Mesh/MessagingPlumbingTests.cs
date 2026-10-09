using Mediator;
using Meshtastic.Protobufs;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.MarkMessageSent;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.SendMessage;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.VerifyRegistration;
using Meshtrail.Core.Application.UseCases.Mesh.Queries.GetMessages;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Mesh;
using Meshtrail.Mesh.Events;
using Meshtrail.Mesh.Outbound;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

/// <summary>Messaging pieces outside the handlers: translation, the outbox, packets, validators.</summary>
[TestClass]
public sealed class MessagingPlumbingTests
{
    [TestMethod]
    public void Translate_TextPacket_KeepsRawText()
    {
        // Arrange
        var translator = new PacketTranslator();
        var message = Packet(HikerNodeNum, PortNum.TextMessageApp, new Position());
        message.Packet.To = uint.MaxValue;
        message.Packet.Decoded.Payload = Google.Protobuf.ByteString.CopyFromUtf8("\u0007SOS");

        // Act
        var text = translator.Translate(message, Now)[1].ShouldBeOfType<TextReceived>();

        // Assert
        text.Text.ShouldBe("\u0007SOS");
        text.To.ShouldBe(uint.MaxValue);
        text.PacketId.ShouldBe(1234u);
        text.HopsAway.ShouldBe(2);
    }

    [TestMethod]
    public void Translate_RoutingReport_ReturnsErrorNameAndAddressee()
    {
        // Arrange
        var translator = new PacketTranslator();

        // Act
        var events = translator.Translate(Packet(HikerNodeNum, PortNum.RoutingApp, new Routing { ErrorReason = Routing.Types.Error.MaxRetransmit }, 77), Now);

        // Assert
        events[1].ShouldBe(new RoutingReceived(Now, HikerNodeNum, GatewayNodeNum, 77, "MaxRetransmit"));
    }

    [TestMethod]
    public async Task Outbox_SameMessageTwice_IsSentOnce()
    {
        // Arrange
        var transport = new RecordingTransport(GatewayTransport.Mqtt, "local");
        using var outbox = Outbox(transport);
        var request = new TextMessageRequest(Route(), HikerNodeNum, 1, Guid.NewGuid(), 0, "hi");

        // Act
        outbox.Enqueue(request);
        outbox.Enqueue(request);
        await transport.WaitForAsync(1);
        await Task.Delay(100);

        // Assert
        transport.Sent.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task Outbox_MqttGateway_SendsFromTheVirtualNodeAndMarksSent()
    {
        // Arrange
        var transport = new RecordingTransport(GatewayTransport.Mqtt, "local");
        var sender = new Mock<ISender>();
        using var outbox = Outbox(transport, sender);
        var messageId = Guid.NewGuid();

        // Act
        outbox.Enqueue(new TextMessageRequest(Route(), HikerNodeNum, 42, messageId, 0, "hi"));
        var (via, packet) = (await transport.WaitForAsync(1))[0];
        await Task.Delay(100);

        // Assert
        via.GatewayNodeNum.ShouldBe(GatewayNodeNum);
        packet.From.ShouldBe(VirtualNodeNum);
        packet.To.ShouldBe(HikerNodeNum);
        packet.Id.ShouldBe(42u);
        sender.Verify(s => s.Send(new MarkMessageSentCommand(messageId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Outbox_TcpGateway_LeavesTheSenderToTheNode()
    {
        // Arrange
        var transport = new RecordingTransport(GatewayTransport.Tcp, null);
        using var outbox = Outbox(transport);

        // Act
        outbox.Enqueue(new PositionRequest(Route() with { Transport = GatewayTransport.Tcp }, HikerNodeNum, 7));
        var (_, packet) = (await transport.WaitForAsync(1))[0];

        // Assert
        packet.From.ShouldBe(0u);
        packet.Decoded.WantResponse.ShouldBeTrue();
    }

    [TestMethod]
    public void TextPacket_AsksForDeliveryReport()
    {
        // Act
        var packet = MeshPackets.Text(HikerNodeNum, 2, "hi", 42);

        // Assert
        packet.WantAck.ShouldBeTrue();
        packet.Channel.ShouldBe(2u);
        packet.Decoded.Portnum.ShouldBe(PortNum.TextMessageApp);
    }

    [TestMethod]
    public void AddContactPacket_GoesToTheGatewayWithTheKey()
    {
        // Act
        var packet = MeshPackets.AddContact(GatewayNodeNum, HikerNodeNum, "Hiker", "HKR", GatewayPublicKey, 42);

        // Assert
        packet.To.ShouldBe(GatewayNodeNum);
        var contact = AdminMessage.Parser.ParseFrom(packet.Decoded.Payload).AddContact;
        contact.NodeNum.ShouldBe(HikerNodeNum);
        contact.User.PublicKey.ToByteArray().ShouldBe(GatewayPublicKey);
        contact.ManuallyVerified.ShouldBeTrue();
    }

    [TestMethod]
    public void SendMessageValidator_BytesNotCharacters_AreCounted()
    {
        // Act
        var result = new SendMessageValidator().Validate(new SendMessageCommand(HikerNodeNum, null, new string('€', 67)));

        // Assert
        result.Errors.ShouldContain(error => error.PropertyName == "Text");
    }

    [TestMethod]
    public void SendMessageValidator_Broadcast_IsInvalid()
    {
        // Act
        var result = new SendMessageValidator().Validate(new SendMessageCommand(uint.MaxValue, null, "hi"));

        // Assert
        result.Errors.ShouldContain(error => error.PropertyName == "ToNodeNum");
    }

    [TestMethod]
    public void SendMessageValidator_BothNodeAndTeam_IsInvalid()
    {
        // Act
        var result = new SendMessageValidator().Validate(new SendMessageCommand(HikerNodeNum, Guid.NewGuid(), "hi"));

        // Assert
        result.Errors.ShouldContain(error => error.PropertyName == "ToNodeNum");
    }

    [TestMethod]
    [DataRow(12345u, false, true)]
    [DataRow(null, true, true)]
    [DataRow(null, false, false)]
    [DataRow(12345u, true, false)]
    [DataRow(0u, false, false)]
    public void GetMessagesValidator_ExactlyOneOfNodeOrTeam(uint? node, bool team, bool valid)
    {
        // Act
        var result = new GetMessagesValidator().Validate(
            new GetMessagesQuery(new MessageListRequest { Node = node, Team = team ? Guid.NewGuid() : null }));

        // Assert
        result.IsValid.ShouldBe(valid);
    }

    [TestMethod]
    [DataRow("123456", true)]
    [DataRow(" 123456 ", true)]
    [DataRow("12345", false)]
    [DataRow("abcdef", false)]
    public void VerifyValidator_CodeIsSixDigits(string code, bool valid)
    {
        // Act
        var result = new VerifyRegistrationValidator().Validate(new VerifyRegistrationCommand(Guid.NewGuid(), code));

        // Assert
        result.IsValid.ShouldBe(valid);
    }

    private static MeshOutbox Outbox(IGatewayTransport transport, Mock<ISender>? sender = null)
    {
        var services = new ServiceCollection().AddSingleton((sender ?? new Mock<ISender>()).Object).BuildServiceProvider();
        return new MeshOutbox(
            [transport],
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new MeshOutboundOptions { MinInterval = TimeSpan.Zero, VirtualNodeNum = VirtualNodeNum }),
            TimeProvider.System,
            NullLogger<MeshOutbox>.Instance);
    }
}
