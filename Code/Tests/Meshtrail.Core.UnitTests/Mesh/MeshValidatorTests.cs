using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Application.UseCases.Map;
using Meshtrail.Core.Application.UseCases.Map.Queries.GetMapFeatures;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestPosition;
using Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodes;
using Meshtrail.Core.Contracts.Map;
using Meshtrail.Core.Contracts.Mesh;
using Moq;
using Shouldly;

namespace Meshtrail.Core.UnitTests.Mesh;

[TestClass]
public sealed class MeshValidatorTests
{
    [TestMethod]
    [DataRow(0u)]
    [DataRow(uint.MaxValue)]
    public void RequestPosition_ZeroOrBroadcast_IsInvalid(uint nodeNum)
    {
        // Act
        var result = new RequestPositionValidator().Validate(new RequestPositionCommand(nodeNum));

        // Assert
        result.Errors.ShouldContain(error => error.PropertyName == "NodeNum");
    }

    [TestMethod]
    public void RequestPosition_RealNode_IsValid()
    {
        // Act + Assert
        new RequestPositionValidator().Validate(new RequestPositionCommand(4044729068)).IsValid.ShouldBeTrue();
    }

    [TestMethod]
    public void GetNodes_PageSizeTooLarge_IsInvalid()
    {
        // Act
        var result = new GetNodesValidator().Validate(new GetNodesQuery(new NodeListRequest { PageSize = GetNodesValidator.MaxPageSize + 1 }));

        // Assert
        result.Errors.ShouldContain(error => error.PropertyName == "Request.PageSize");
    }

    [TestMethod]
    [DataRow("nodes", null, true)]
    [DataRow("NODES, nodes", null, true)]
    [DataRow("nodes,unicorns", null, false)]
    [DataRow(null, "2.5,49.5,6.4,51.5", true)]
    [DataRow(null, "2.5,49.5,6.4", false)]
    [DataRow(null, "2.5,52,6.4,51", false)]
    [DataRow(null, "a,b,c,d", false)]
    public void GetMapFeatures_ChecksLayersAndBbox(string? layers, string? bbox, bool valid)
    {
        // Arrange
        var nodesLayer = new Mock<IMapLayerSource>();
        nodesLayer.SetupGet(layer => layer.Layer).Returns(MapLayers.Nodes);
        var validator = new GetMapFeaturesValidator([nodesLayer.Object]);

        // Act
        var result = validator.Validate(new GetMapFeaturesQuery(new MapFeaturesRequest { Layers = layers, Bbox = bbox }));

        // Assert
        result.IsValid.ShouldBe(valid);
    }

    [TestMethod]
    public void BoundingBox_CrossingDateLine_IsAllowed()
    {
        // Act
        var parsed = BoundingBox.TryParse("170,-10,-170,10", out var box);

        // Assert
        parsed.ShouldBeTrue();
        box.West.ShouldBe(170);
        box.East.ShouldBe(-170);
    }
}
