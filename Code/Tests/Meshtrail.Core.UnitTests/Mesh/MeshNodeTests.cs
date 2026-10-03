using Meshtrail.Core.Domain.Mesh;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

[TestClass]
public sealed class MeshNodeTests
{
    [TestMethod]
    public void Discover_NewNode_UsesFirmwareDefaultNames()
    {
        // Act
        var node = MeshNode.Discover(GatewayNodeNum, Now);

        // Assert
        node.NodeId.ShouldBe(GatewayNodeId);
        node.LongName.ShouldBe("Meshtastic aaec");
        node.ShortName.ShouldBe("aaec");
        node.Source.ShouldBe(MeshNode.MeshSource);
        node.FirstSeenAt.ShouldBe(Now);
        node.LastHeardAt.ShouldBeNull();
    }

    [TestMethod]
    public void ApplyUser_UntrustedNames_AreCleanedAndCut()
    {
        // Arrange
        var node = KnownNode();
        var longName = "Evil\u0007\nName " + new string('x', 60);

        // Act
        node.ApplyUser(longName, "  AB\u0000C  ", "M5StackC6L", "Client", null, Now);

        // Assert
        node.LongName.ShouldStartWith("Evil Name x");
        node.LongName.Length.ShouldBe(MeshNode.LongNameMaxLength);
        node.ShortName.ShouldBe("ABC");
    }

    [TestMethod]
    public void ApplyUser_MissingValues_KeepPreviousOnes()
    {
        // Arrange
        var node = KnownNode();
        node.ApplyUser("Hiker", "HKR", "TEcho", "Client", GatewayPublicKey, Now);

        // Act
        node.ApplyUser(null, " ", null, null, null, Now);

        // Assert
        node.LongName.ShouldBe("Hiker");
        node.ShortName.ShouldBe("HKR");
        node.HardwareModel.ShouldBe("TEcho");
        node.PublicKey.ShouldBe(GatewayPublicKey);
    }

    [TestMethod]
    public void ApplyUser_KeyOfWrongLength_IsIgnored()
    {
        // Arrange
        var node = KnownNode();

        // Act
        node.ApplyUser("Hiker", null, null, null, [1, 2, 3], Now);

        // Assert
        node.PublicKey.ShouldBeNull();
    }

    [TestMethod]
    public void RecordHeard_OlderThanLastHeard_IsIgnored()
    {
        // Arrange
        var node = KnownNode();
        node.RecordHeard(Now, 5.5, -80, 1, Now);

        // Act
        node.RecordHeard(Now.AddMinutes(-10), -3, -120, 4, Now);

        // Assert
        node.LastHeardAt.ShouldBe(Now);
        node.Snr.ShouldBe(5.5);
        node.HopsAway.ShouldBe(1);
    }

    [TestMethod]
    public void RecordHeard_TimeFarInTheFuture_UsesOurClock()
    {
        // Arrange
        var node = KnownNode();

        // Act
        node.RecordHeard(Now.AddDays(3), null, null, null, Now);

        // Assert
        node.LastHeardAt.ShouldBe(Now);
    }

    [TestMethod]
    public void RecordHeard_UnknownSignal_KeepsLastMeasuredValues()
    {
        // Arrange
        var node = KnownNode();
        node.RecordHeard(Now.AddMinutes(-1), 4.0, -90, 2, Now);

        // Act
        node.RecordHeard(Now, null, null, null, Now);

        // Assert
        node.LastHeardAt.ShouldBe(Now);
        node.Snr.ShouldBe(4.0);
        node.Rssi.ShouldBe(-90);
        node.HopsAway.ShouldBe(2);
    }

    [TestMethod]
    public void RecordPosition_OlderFix_IsRejected()
    {
        // Arrange
        var node = KnownNode();
        node.RecordPosition(Fix(50, 4, Now), Now);

        // Act
        var accepted = node.RecordPosition(Fix(51, 5, Now.AddMinutes(-5)), Now);

        // Assert
        accepted.ShouldBeFalse();
        node.LastPosition!.Latitude.ShouldBe(50);
    }

    [TestMethod]
    public void RecordPosition_NewerFix_ReplacesLastPosition()
    {
        // Arrange
        var node = KnownNode();
        node.RecordPosition(Fix(50, 4, Now.AddMinutes(-5)), Now);

        // Act
        var accepted = node.RecordPosition(Fix(51, 5, Now), Now);

        // Assert
        accepted.ShouldBeTrue();
        node.LastPosition!.Latitude.ShouldBe(51);
    }

    [TestMethod]
    [DataRow(150, 101)]
    [DataRow(-5, 0)]
    [DataRow(55, 55)]
    public void RecordTelemetry_BatteryLevel_IsClamped(int reported, int stored)
    {
        // Arrange
        var node = KnownNode();

        // Act
        node.RecordTelemetry(reported, null, Now);

        // Assert
        node.BatteryLevel.ShouldBe(stored);
    }

    [TestMethod]
    public void IsExternalPower_Battery101_IsTrue()
    {
        // Arrange
        var node = KnownNode();

        // Act
        node.RecordTelemetry(MeshNode.ExternalPowerLevel, 0, Now);

        // Assert
        node.IsExternalPower.ShouldBeTrue();
        node.Voltage.ShouldBeNull();
    }

    [TestMethod]
    [DataRow(14, true)]
    [DataRow(16, false)]
    public void IsOnline_DependsOnLastHeard(int minutesAgo, bool expected)
    {
        // Arrange
        var node = KnownNode();
        node.RecordHeard(Now.AddMinutes(-minutesAgo), null, null, null, Now);

        // Act + Assert
        node.IsOnline(Now).ShouldBe(expected);
    }

    [TestMethod]
    public void IsOnline_NeverHeard_IsFalse()
    {
        // Act + Assert
        KnownNode().IsOnline(Now).ShouldBeFalse();
    }
}
