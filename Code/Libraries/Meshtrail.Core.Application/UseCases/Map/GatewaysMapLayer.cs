using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Map;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Map;

/// <summary>Map layer with the gateways (at their node's position), coloured by status in the client.</summary>
public sealed class GatewaysMapLayer(IMeshGatewayRepository gateways, IMeshNodeRepository nodes) : IMapLayerSource
{
    public string Layer => MapLayers.Gateways;

    public async Task<IReadOnlyList<MapFeatureDto>> GetFeaturesAsync(BoundingBox? box, CancellationToken cancellationToken)
    {
        var bound = await gateways.GetBoundAsync(cancellationToken);
        var located = (await nodes.GetManyAsync([.. bound.Select(gateway => gateway.NodeNum!.Value)], cancellationToken))
            .Where(node => node.LastPosition is not null && (box is null || box.Contains(node.LastPosition.Latitude, node.LastPosition.Longitude)))
            .ToDictionary(node => node.NodeNum);

        return [.. bound
            .Where(gateway => located.ContainsKey(gateway.NodeNum!.Value))
            .Select(gateway => ToFeature(gateway, located[gateway.NodeNum!.Value]))];
    }

    private MapFeatureDto ToFeature(MeshGateway gateway, MeshNode node)
    {
        var position = node.LastPosition!;
        return new MapFeatureDto(
            $"{Layer}:{gateway.Id}",
            new MapPointDto([position.Longitude, position.Latitude]),
            new Dictionary<string, object?>
            {
                ["layer"] = Layer,
                ["source"] = "gateway",
                ["gatewayId"] = gateway.Id,
                ["nodeNum"] = node.NodeNum,
                ["nodeId"] = node.NodeId,
                ["longName"] = node.LongName,
                ["transport"] = gateway.Transport.ToString(),
                ["status"] = gateway.Status.ToString(),
                ["lastUplinkAt"] = gateway.LastUplinkAt,
            });
    }
}
