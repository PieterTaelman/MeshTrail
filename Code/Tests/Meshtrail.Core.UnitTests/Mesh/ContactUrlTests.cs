using Meshtastic.Protobufs;
using Meshtrail.Mesh;
using Meshtrail.Mesh.Contacts;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

[TestClass]
public sealed class ContactUrlTests
{
    [TestMethod]
    public void TryParse_GatewayContactLink_ReturnsNodeAndUser()
    {
        // Act
        var parsed = ContactUrl.TryParse(GatewayContactUrl, out var contact, out var error);

        // Assert
        parsed.ShouldBeTrue(error);
        contact.NodeNum.ShouldBe(GatewayNodeNum);
        NodeIds.Format(contact.NodeNum).ShouldBe(GatewayNodeId);
        contact.User.Id.ShouldBe(GatewayNodeId);
        contact.User.LongName.ShouldBe("Node 2109");
        contact.User.ShortName.ShouldBe("2109");
        contact.User.HwModel.ShouldBe(HardwareModel.M5StackC6L);
        contact.User.PublicKey.ToByteArray().ShouldBe(GatewayPublicKey);
    }

    [TestMethod]
    public void Create_GatewayContact_ProducesTheAppsLink()
    {
        // Act
        var url = ContactUrl.Create(GatewayContact());

        // Assert
        url.ShouldBe(GatewayContactUrl);
    }

    [TestMethod]
    public void TryParse_SurroundingWhitespaceAndPadding_IsAccepted()
    {
        // Act
        var parsed = ContactUrl.TryParse($"  {GatewayContactUrl}==  ", out var contact, out _);

        // Assert
        parsed.ShouldBeTrue();
        contact.NodeNum.ShouldBe(GatewayNodeNum);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("not a url")]
    [DataRow("https://example.org/v/#COzV1ogP")]
    [DataRow("http://meshtastic.org/v/#COzV1ogP")]
    [DataRow("https://meshtastic.org/e/#COzV1ogP")]
    [DataRow("https://meshtastic.org/v/")]
    public void TryParse_NotAContactLink_ReturnsFalseWithError(string? url)
    {
        // Act
        var parsed = ContactUrl.TryParse(url, out _, out var error);

        // Assert
        parsed.ShouldBeFalse();
        error.ShouldNotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public void TryParse_BrokenBase64_ReturnsFalse()
    {
        // Act
        var parsed = ContactUrl.TryParse("https://meshtastic.org/v/#***", out _, out var error);

        // Assert
        parsed.ShouldBeFalse();
        error.ShouldContain("base64");
    }

    [TestMethod]
    public void TryParse_NoNodeNumber_ReturnsFalse()
    {
        // Arrange
        var url = ContactUrl.Create(new SharedContact { User = new User { LongName = "Nobody" } });

        // Act
        var parsed = ContactUrl.TryParse(url, out _, out var error);

        // Assert
        parsed.ShouldBeFalse();
        error.ShouldContain("node number");
    }
}
