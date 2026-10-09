using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Map;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Map;

/// <summary>Map layer with the mesh nodes that have a known position (in the requested box).</summary>
public sealed class NodesMapLayer(
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    INodeRegistrationRepository registrations,
    TimeProvider timeProvider) : IMapLayerSource
{
    /// <summary>A zoomed-out world map gets the most recently heard nodes only; zooming in shows the rest.</summary>
    public const int MaxFeatures = 5000;

    public string Layer => MapLayers.Nodes;

    public async Task<IReadOnlyList<MapFeatureDto>> GetFeaturesAsync(BoundingBox? box, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var withPosition = await nodes.GetWithPositionAsync(box, MaxFeatures, cancellationToken);
        var nodeNums = withPosition.Select(node => node.NodeNum).ToList();
        var registered = await registrations.GetVerifiedNodeNumsAsync(nodeNums, cancellationToken);
        var gatewayNodes = (await gateways.GetActiveByNodeNumsAsync(nodeNums, cancellationToken)).Select(gateway => gateway.NodeNum!.Value).ToHashSet();

        return [.. withPosition.Select(node => ToFeature(node, now, gatewayNodes.Contains(node.NodeNum), registered.Contains(node.NodeNum)))];
    }

    private MapFeatureDto ToFeature(MeshNode node, DateTimeOffset now, bool isGateway, bool isRegistered)
    {
        var position = node.LastPosition!;
        return new MapFeatureDto(
            $"{Layer}:{node.NodeNum}",
            new MapPointDto([position.Longitude, position.Latitude]),
            new Dictionary<string, object?>
            {
                ["layer"] = Layer,
                ["source"] = node.Source,
                ["nodeNum"] = node.NodeNum,
                ["nodeId"] = node.NodeId,
                ["longName"] = node.LongName,
                ["shortName"] = node.ShortName,
                ["lastHeardAt"] = node.LastHeardAt,
                ["isOnline"] = node.IsOnline(now),
                ["isGateway"] = isGateway,
                ["isRegistered"] = isRegistered,
                ["batteryLevel"] = node.BatteryLevel,
                ["isExternalPower"] = node.IsExternalPower,
                ["positionTime"] = position.Time,
                ["precisionBits"] = position.PrecisionBits,
            });
    }
}
