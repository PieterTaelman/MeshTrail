using Meshtrail.Mesh.Mqtt;
using Shouldly;

namespace Meshtrail.Core.UnitTests.Mesh;

[TestClass]
public sealed class MeshtasticTopicTests
{
    [TestMethod]
    public void TryParse_EnvelopeTopicWithRegionRoot_SplitsRootChannelAndGateway()
    {
        // Act
        var parsed = MeshtasticTopic.TryParse("msh/EU_868/2/e/LongFast/!f115aaec", out var topic);

        // Assert
        parsed.ShouldBeTrue();
        topic.ShouldBe(new MeshtasticTopic("msh/EU_868", MeshtasticTopicKind.Envelope, "LongFast", "!f115aaec"));
    }

    [TestMethod]
    [DataRow("msh/2/json/LongFast/!f115aaec", "msh", MeshtasticTopicKind.Json)]
    [DataRow("msh/EU_868/2/map/", "msh/EU_868", MeshtasticTopicKind.Map)]
    [DataRow("custom/root/2/stat/!f115aaec", "custom/root", MeshtasticTopicKind.Status)]
    public void TryParse_OtherKinds_AreRecognised(string raw, string root, MeshtasticTopicKind kind)
    {
        // Act
        var parsed = MeshtasticTopic.TryParse(raw, out var topic);

        // Assert
        parsed.ShouldBeTrue();
        topic.Root.ShouldBe(root);
        topic.Kind.ShouldBe(kind);
    }

    [TestMethod]
    public void TryParse_StatusTopic_HasGatewayId()
    {
        // Act
        MeshtasticTopic.TryParse("msh/EU_868/2/stat/!f115aaec", out var topic);

        // Assert
        topic.GatewayId.ShouldBe("!f115aaec");
        topic.Channel.ShouldBeNull();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("home/temperature")]
    [DataRow("msh/EU_868/2/x/LongFast/!f115aaec")]
    public void TryParse_NotMeshtastic_ReturnsFalse(string? raw)
    {
        // Act + Assert
        MeshtasticTopic.TryParse(raw, out _).ShouldBeFalse();
    }

    [TestMethod]
    public void ForEnvelope_BuildsTheTopicGatewaysListenTo()
    {
        // Act + Assert
        MeshtasticTopic.ForEnvelope("msh/EU_868", "LongFast", "!4d545231").ShouldBe("msh/EU_868/2/e/LongFast/!4d545231");
    }
}
