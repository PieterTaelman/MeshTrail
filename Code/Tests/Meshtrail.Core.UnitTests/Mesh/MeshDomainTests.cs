using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

/// <summary>Rules of the smaller Mesh domain types: gateway, traceroute, position and untrusted text.</summary>
[TestClass]
public sealed class MeshDomainTests
{
    [TestMethod]
    public void Gateway_ChangeStatus_SameValues_ReturnsFalse()
    {
        // Arrange
        var gateway = Gateway(GatewayStatus.Offline);

        // Act
        var changed = gateway.ChangeStatus(GatewayStatus.Offline, "Tcp", "test", Now);

        // Assert
        changed.ShouldBeFalse();
    }

    [TestMethod]
    public void Gateway_ChangeStatus_Online_ClearsErrorAndSetsLastConnected()
    {
        // Arrange
        var gateway = Gateway(GatewayStatus.Offline);

        // Act
        var changed = gateway.ChangeStatus(GatewayStatus.Online, "Tcp", "ignored", Now);

        // Assert
        changed.ShouldBeTrue();
        gateway.LastError.ShouldBeNull();
        gateway.LastConnectedAt.ShouldBe(Now);
        gateway.StatusChangedAt.ShouldBe(Now);
    }

    [TestMethod]
    [DataRow(GatewayStatus.Offline)]
    [DataRow(GatewayStatus.Connecting)]
    public void Gateway_EnsureCanSend_NotOnline_Throws(GatewayStatus status)
    {
        // Act + Assert
        Should.Throw<DomainException>(() => MeshGateway.EnsureCanSend(Gateway(status)));
    }

    [TestMethod]
    public void Gateway_EnsureCanSend_NoGatewayYet_Throws()
    {
        // Act + Assert
        Should.Throw<DomainException>(() => MeshGateway.EnsureCanSend(null));
    }

    [TestMethod]
    public void Traceroute_NoAnswerAfterTimeout_IsTimedOut()
    {
        // Arrange
        var traceroute = NodeTraceroute.Request(HikerNodeNum, 42, UserName, Now);

        // Act
        var status = traceroute.GetStatus(Now + NodeTraceroute.Timeout + TimeSpan.FromSeconds(1));

        // Assert
        status.ShouldBe(TracerouteStatus.TimedOut);
    }

    [TestMethod]
    public void Traceroute_CompleteTwice_SecondAnswerIsIgnored()
    {
        // Arrange
        var traceroute = NodeTraceroute.Request(HikerNodeNum, 42, UserName, Now);
        traceroute.Complete([1], [6.0], [], [], Now);

        // Act
        var accepted = traceroute.Complete([2, 3], [1.0], [], [], Now.AddSeconds(5));

        // Assert
        accepted.ShouldBeFalse();
        traceroute.RouteTowards.ShouldBe([1u]);
        traceroute.GetStatus(Now.AddHours(1)).ShouldBe(TracerouteStatus.Completed);
    }

    [TestMethod]
    public void Traceroute_TooManyHops_AreCut()
    {
        // Arrange
        var traceroute = NodeTraceroute.Request(HikerNodeNum, 42, UserName, Now);

        // Act
        traceroute.Complete([.. Enumerable.Range(1, 20).Select(i => (uint)i)], [], [], [], Now);

        // Assert
        traceroute.RouteTowards.Count.ShouldBe(NodeTraceroute.MaxHops);
    }

    [TestMethod]
    public void Traceroute_ZeroPacketId_Throws()
    {
        // Act + Assert
        Should.Throw<DomainException>(() => NodeTraceroute.Request(HikerNodeNum, 0, UserName, Now));
    }

    [TestMethod]
    [DataRow(91.0, 4.0)]
    [DataRow(50.0, -181.0)]
    [DataRow(double.NaN, 4.0)]
    public void GeoPosition_OutOfRange_Throws(double latitude, double longitude)
    {
        // Act + Assert
        Should.Throw<DomainException>(() => GeoPosition.Create(latitude, longitude, null, Now, 32));
    }

    [TestMethod]
    [DataRow(null, null)]
    [DataRow("   ", null)]
    [DataRow("\u0007SOS\u0007", "SOS")]
    [DataRow("line1\nline2", "line1 line2")]
    public void UntrustedText_Clean_RemovesControlCharacters(string? input, string? expected)
    {
        // Act + Assert
        UntrustedText.Clean(input, 40).ShouldBe(expected);
    }

    [TestMethod]
    public void UntrustedText_Clean_DoesNotCutEmojiInHalf()
    {
        // Arrange: "ab" + 🙂 (2 UTF-16 chars) = 4 chars; cutting at 3 would split the emoji.
        var input = "ab\U0001F642";

        // Act
        var cleaned = UntrustedText.Clean(input, 3);

        // Assert
        cleaned.ShouldBe("ab");
    }
}
