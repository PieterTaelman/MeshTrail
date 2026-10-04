using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Map;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Map;

/// <summary>Map layer with every mesh node that has a known position.</summary>
public sealed class NodesMapLayer(
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    INodeRegistrationRepository registrations,
    TimeProvider timeProvider) : IMapLayerSource
{
    public string Layer => MapLayers.Nodes;

    public async Task<IReadOnlyList<MapFeatureDto>> GetFeaturesAsync(BoundingBox? box, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var gateway = await gateways.GetAsync(MeshGateway.PrimaryKey, cancellationToken);
        var withPosition = await nodes.GetWithPositionAsync(box, cancellationToken);
        var registered = await registrations.GetVerifiedNodeNumsAsync([.. withPosition.Select(node => node.NodeNum)], cancellationToken);

        return [.. withPosition.Select(node => ToFeature(node, now, gateway?.NodeNum, registered.Contains(node.NodeNum)))];
    }

    private MapFeatureDto ToFeature(MeshNode node, DateTimeOffset now, uint? gatewayNodeNum, bool isRegistered)
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
                ["isGateway"] = node.NodeNum == gatewayNodeNum,
                ["isRegistered"] = isRegistered,
                ["batteryLevel"] = node.BatteryLevel,
                ["isExternalPower"] = node.IsExternalPower,
                ["positionTime"] = position.Time,
                ["precisionBits"] = position.PrecisionBits,
            });
    }
}
