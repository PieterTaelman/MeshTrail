using Meshtastic.Protobufs;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.ReceiveTextMessage;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordRoutingResult;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.SendMessage;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.VerifyRegistration;
using Meshtrail.Core.Application.UseCases.Mesh.Queries.GetMessages;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Infrastructure.Mesh;
using Meshtrail.Mesh.Events;
using Meshtrail.Mesh.Outbound;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

/// <summary>Messaging pieces outside the handlers: translation, queue de-duplication, packets, validators.</summary>
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
    public void Translate_RoutingReport_ReturnsErrorName()
    {
        // Arrange
        var translator = new PacketTranslator();

        // Act
        var events = translator.Translate(Packet(HikerNodeNum, PortNum.RoutingApp, new Routing { ErrorReason = Routing.Types.Error.MaxRetransmit }, 77), Now);

        // Assert
        events[1].ShouldBe(new RoutingReceived(Now, HikerNodeNum, 77, "MaxRetransmit"));
    }

    [TestMethod]
    public void ToCommand_TextAndRouting_MapToCommands()
    {
        // Act
        var text = MeshEventCommands.ToCommand(new TextReceived(Now, HikerNodeNum, uint.MaxValue, 0, "hi", 5, 1.0, -90, 1));
        var routing = MeshEventCommands.ToCommand(new RoutingReceived(Now, HikerNodeNum, 5, "None"));

        // Assert
        text.ShouldBe(new ReceiveTextMessageCommand(HikerNodeNum, uint.MaxValue, 0, "hi", 5, 1.0, -90, 1, Now));
        routing.ShouldBe(new RecordRoutingResultCommand(HikerNodeNum, 5, "None"));
    }

    [TestMethod]
    public async Task GatewayService_SameMessageTwice_IsQueuedOnce()
    {
        // Arrange
        var service = new MeshGatewayService();
        var request = new TextMessageRequest(HikerNodeNum, 1, Guid.NewGuid(), 0, "hi");

        // Act
        service.Enqueue(request);
        service.Enqueue(request);

        // Assert
        (await service.Outbound.ReadAsync()).ShouldBe(request);
        service.Outbound.TryRead(out _).ShouldBeFalse();
    }

    [TestMethod]
    public async Task GatewayService_AfterComplete_MessageCanBeQueuedAgain()
    {
        // Arrange
        var service = new MeshGatewayService();
        var request = new TextMessageRequest(HikerNodeNum, 1, Guid.NewGuid(), 0, "hi");
        service.Enqueue(request);
        service.Complete(await service.Outbound.ReadAsync());

        // Act
        service.Enqueue(request);

        // Assert
        service.Outbound.TryRead(out var again).ShouldBeTrue();
        again.ShouldBe(request);
    }

    [TestMethod]
    public void TextPacket_AsksForDeliveryReport()
    {
        // Act
        var packet = MeshPackets.Text(HikerNodeNum, 2, "hi", 42).Packet;

        // Assert
        packet.WantAck.ShouldBeTrue();
        packet.Channel.ShouldBe(2u);
        packet.Decoded.Portnum.ShouldBe(PortNum.TextMessageApp);
    }

    [TestMethod]
    public void AddContactPacket_GoesToOurGatewayWithTheKey()
    {
        // Act
        var packet = MeshPackets.AddContact(GatewayNodeNum, HikerNodeNum, "Hiker", "HKR", GatewayPublicKey, 42).Packet;

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
        var result = new SendMessageValidator().Validate(new SendMessageCommand(0, null, new string('€', 67)));

        // Assert
        result.Errors.ShouldContain(error => error.PropertyName == "Text");
    }

    [TestMethod]
    [DataRow(0, null, true)]
    [DataRow(null, 12345u, true)]
    [DataRow(null, null, false)]
    [DataRow(0, 12345u, false)]
    public void GetMessagesValidator_ExactlyOneOfChannelOrNode(int? channel, uint? node, bool valid)
    {
        // Act
        var result = new GetMessagesValidator().Validate(new GetMessagesQuery(new MessageListRequest { Channel = channel, Node = node }));

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
}
