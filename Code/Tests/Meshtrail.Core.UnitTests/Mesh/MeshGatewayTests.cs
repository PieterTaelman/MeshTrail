using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

/// <summary>Gateway rules: credentials, binding to a node, status; receptions and picking the gateway to send through.</summary>
[TestClass]
public sealed class MeshGatewayTests
{
    [TestMethod]
    public void IssueMqtt_NewGateway_IsPendingOnTheChosenNodeAndKeepsOnlyAHash()
    {
        // Act
        var gateway = PendingGateway();

        // Assert
        gateway.Status.ShouldBe(GatewayStatus.Pending);
        gateway.NodeNum.ShouldBe(GatewayNodeNum);
        gateway.CanSend.ShouldBeFalse();
        gateway.CredentialHash.ShouldNotBeNull().Length.ShouldBe(MeshGateway.CredentialHashLength);
    }

    [TestMethod]
    [DataRow(GatewayPassword, true)]
    [DataRow("wrong", false)]
    [DataRow(null, false)]
    public void PasswordMatches_ChecksThePassword(string? password, bool expected)
    {
        // Act + Assert
        PendingGateway().PasswordMatches(password).ShouldBe(expected);
    }

    [TestMethod]
    public void PasswordMatches_RevokedGateway_IsFalse()
    {
        // Arrange
        var gateway = PendingGateway();
        gateway.Revoke(Now);

        // Act + Assert
        gateway.PasswordMatches(GatewayPassword).ShouldBeFalse();
    }

    [TestMethod]
    public void Bind_FirstUplink_TiesTheGatewayToTheNodeAndGoesOnline()
    {
        // Arrange
        var gateway = PendingGateway();

        // Act
        var result = gateway.Bind(GatewayNodeNum, Now);

        // Assert
        result.ShouldBe(GatewayBindResult.Bound);
        gateway.NodeNum.ShouldBe(GatewayNodeNum);
        gateway.Status.ShouldBe(GatewayStatus.Online);
        gateway.CanSend.ShouldBeTrue();
    }

    [TestMethod]
    public void Bind_LoginUsedByAnotherNode_IsRefusedAndKeepsTheFirstNode()
    {
        // Arrange
        var gateway = BoundGateway();

        // Act
        var result = gateway.Bind(OtherGatewayNodeNum, Now);

        // Assert
        result.ShouldBe(GatewayBindResult.OtherNode);
        gateway.NodeNum.ShouldBe(GatewayNodeNum);
    }

    [TestMethod]
    public void Bind_PendingLoginUsedByAnotherNode_IsRefusedAndStaysPending()
    {
        // Arrange
        var gateway = PendingGateway();

        // Act
        var result = gateway.Bind(OtherGatewayNodeNum, Now);

        // Assert
        result.ShouldBe(GatewayBindResult.OtherNode);
        gateway.Status.ShouldBe(GatewayStatus.Pending);
    }

    [TestMethod]
    public void Bind_Revoked_IsRefused()
    {
        // Arrange
        var gateway = PendingGateway();
        gateway.Revoke(Now);

        // Act + Assert
        gateway.Bind(GatewayNodeNum, Now).ShouldBe(GatewayBindResult.Revoked);
    }

    [TestMethod]
    public void RecordUplink_RemembersRootAndFirstChannelOnly()
    {
        // Arrange
        var gateway = PendingGateway();
        gateway.Bind(GatewayNodeNum, Now);

        // Act
        gateway.RecordUplink("msh/EU_868", "LongFast", Now);
        gateway.RecordUplink("msh/EU_868", "TeamAlpha", Now.AddMinutes(1));

        // Assert
        gateway.MqttRoot.ShouldBe("msh/EU_868");
        gateway.DownlinkChannel.ShouldBe("LongFast");
        gateway.LastUplinkAt.ShouldBe(Now.AddMinutes(1));
    }

    [TestMethod]
    public void ChangeConnection_PendingGateway_StaysPending()
    {
        // Arrange
        var gateway = PendingGateway();

        // Act
        var changed = gateway.ChangeConnection(true, null, Now);

        // Assert
        changed.ShouldBeFalse();
        gateway.Status.ShouldBe(GatewayStatus.Pending);
    }

    [TestMethod]
    public void ChangeConnection_Disconnected_GoesOfflineWithError()
    {
        // Arrange
        var gateway = BoundGateway();

        // Act
        var changed = gateway.ChangeConnection(false, "Not connected to the broker.", Now);

        // Assert
        changed.ShouldBeTrue();
        gateway.Status.ShouldBe(GatewayStatus.Offline);
        gateway.LastError.ShouldBe("Not connected to the broker.");
        gateway.CanSend.ShouldBeFalse();
    }

    [TestMethod]
    public void Local_MqttTransport_Throws()
    {
        // Act + Assert
        Should.Throw<DomainException>(() => MeshGateway.Local(GatewayTransport.Mqtt, GatewayNodeNum, Now));
    }

    [TestMethod]
    public void Reception_OlderReport_IsIgnored()
    {
        // Arrange
        var reception = Heard(minutesAgo: 1);

        // Act
        var updated = reception.Update(Now.AddMinutes(-10), 1, -100, 3, Now);

        // Assert
        updated.ShouldBeFalse();
        reception.HopsAway.ShouldBe(1);
    }

    [TestMethod]
    public void Routing_FreshFewerHops_WinsOverOlderBetterSignal()
    {
        // Arrange
        var fresh = Heard(GatewayNodeNum, minutesAgo: 5, hops: 2, snr: -5);
        var older = Heard(OtherGatewayNodeNum, minutesAgo: 120, hops: 0, snr: 10);

        // Act
        var best = GatewayRouting.Pick([older, fresh], new HashSet<uint> { GatewayNodeNum, OtherGatewayNodeNum }, Now);

        // Assert
        best.ShouldBe(fresh);
    }

    [TestMethod]
    public void Routing_SameFreshness_FewestHopsThenBestSignal()
    {
        // Arrange
        var twoHops = Heard(GatewayNodeNum, minutesAgo: 1, hops: 2, snr: 9);
        var direct = Heard(OtherGatewayNodeNum, minutesAgo: 10, hops: 0, snr: -3);

        // Act
        var best = GatewayRouting.Pick([twoHops, direct], new HashSet<uint> { GatewayNodeNum, OtherGatewayNodeNum }, Now);

        // Assert
        best.ShouldBe(direct);
    }

    [TestMethod]
    public void Routing_OfflineGateway_IsNotUsed()
    {
        // Arrange
        var viaOffline = Heard(GatewayNodeNum, minutesAgo: 1, hops: 0);
        var viaOnline = Heard(OtherGatewayNodeNum, minutesAgo: 20, hops: 2);

        // Act
        var best = GatewayRouting.Pick([viaOffline, viaOnline], new HashSet<uint> { OtherGatewayNodeNum }, Now);

        // Assert
        best.ShouldBe(viaOnline);
    }

    [TestMethod]
    public void Routing_HeardTooLongAgo_ThrowsNoGatewayCanReach()
    {
        // Arrange
        var stale = Heard(GatewayNodeNum, minutesAgo: GatewayRouting.ReachWindow.TotalMinutes + 1);

        // Act + Assert
        var exception = Should.Throw<DomainException>(() =>
            GatewayRouting.PickOrThrow(HikerNodeNum, [stale], new HashSet<uint> { GatewayNodeNum }, Now));
        exception.Message.ShouldStartWith("No gateway can reach node !0aa00001");
    }
}
