using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

[TestClass]
public sealed class MeshMessageTests
{
    [TestMethod]
    public void QueueOutbound_Valid_IsQueuedAndTrimmed()
    {
        // Act
        var message = MeshMessage.QueueOutbound(0, HikerNodeNum, "  hi  ", MessageKind.Text, 1, GatewayNodeNum, UserName, Now);

        // Assert
        message.Status.ShouldBe(MessageStatus.Queued);
        message.Text.ShouldBe("hi");
        message.PeerNodeNum.ShouldBe(HikerNodeNum);
    }

    [TestMethod]
    public void QueueOutbound_200Bytes_IsAllowed()
    {
        // Act + Assert (100 × "é" = 200 bytes)
        Should.NotThrow(() => MeshMessage.QueueOutbound(0, null, new string('é', 100), MessageKind.Text, 1, null, UserName, Now));
    }

    [TestMethod]
    public void QueueOutbound_201Bytes_Throws()
    {
        // Act + Assert (67 × "€" = 201 bytes)
        Should.Throw<DomainException>(() => MeshMessage.QueueOutbound(0, null, new string('€', 67), MessageKind.Text, 1, null, UserName, Now));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(8)]
    public void QueueOutbound_ChannelOutOfRange_Throws(int channel)
    {
        // Act + Assert
        Should.Throw<DomainException>(() => MeshMessage.QueueOutbound(channel, null, "hi", MessageKind.Text, 1, null, UserName, Now));
    }

    [TestMethod]
    public void HappyPath_QueuedSentAcked()
    {
        // Arrange
        var message = QueuedMessage();

        // Act
        message.MarkSent(GatewayNodeNum, Now);
        message.MarkAcked(Now.AddSeconds(5));

        // Assert
        message.Status.ShouldBe(MessageStatus.Acked);
        message.SentAt.ShouldBe(Now);
        message.AckedAt.ShouldBe(Now.AddSeconds(5));
    }

    [TestMethod]
    public void MarkAcked_StillQueued_Throws()
    {
        // Act + Assert
        Should.Throw<DomainException>(() => QueuedMessage().MarkAcked(Now));
    }

    [TestMethod]
    public void MarkFailed_AfterAcked_Throws()
    {
        // Arrange
        var message = QueuedMessage();
        message.MarkSent(GatewayNodeNum, Now);
        message.MarkAcked(Now);

        // Act + Assert
        Should.Throw<DomainException>(() => message.MarkFailed("late error"));
    }

    [TestMethod]
    public void MarkFailed_Sent_StoresReason()
    {
        // Arrange
        var message = QueuedMessage();
        message.MarkSent(GatewayNodeNum, Now);

        // Act
        message.MarkFailed("MaxRetransmit");

        // Assert
        message.Status.ShouldBe(MessageStatus.Failed);
        message.FailureReason.ShouldBe("MaxRetransmit");
    }

    [TestMethod]
    public void MarkSent_InboundMessage_Throws()
    {
        // Arrange
        var message = MeshMessage.Received(HikerNodeNum, null, 0, "hi", 9, null, null, null, Now);

        // Act + Assert
        Should.Throw<DomainException>(() => message.MarkSent(GatewayNodeNum, Now));
    }

    [TestMethod]
    public void Received_UntrustedText_IsCleaned()
    {
        // Act
        var message = MeshMessage.Received(HikerNodeNum, GatewayNodeNum, 0, "\u0007SOS\nhelp", 9, 5.5, -80, 1, Now);

        // Assert
        message.Text.ShouldBe("SOS help");
        message.Status.ShouldBe(MessageStatus.Received);
        message.PeerNodeNum.ShouldBe(HikerNodeNum);
    }
}
