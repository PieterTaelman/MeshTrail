using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodeById;

/// <summary>The node, its latest traceroute and every gateway that heard it, the one we would send through first.</summary>
public sealed class GetNodeByIdHandler(
    MeshNodeStores stores,
    INodeTracerouteRepository traceroutes,
    TimeProvider timeProvider) : IQueryHandler<GetNodeByIdQuery, NodeDetailDto>
{
    public async ValueTask<NodeDetailDto> Handle(GetNodeByIdQuery query, CancellationToken cancellationToken)
    {
        var node = await stores.Nodes.GetAsync(query.NodeNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Node {MeshNode.FormatNodeId(query.NodeNum)} is not known.");

        var now = timeProvider.GetUtcNow();
        var traceroute = await traceroutes.GetLatestForNodeAsync(query.NodeNum, cancellationToken);
        var registered = await stores.Registrations.GetVerifiedNodeNumsAsync([node.NodeNum], cancellationToken);
        var isGateway = (await stores.Gateways.GetActiveByNodeNumsAsync([node.NodeNum], cancellationToken)).Count > 0;
        var heardBy = await HeardByAsync(node.NodeNum, now, cancellationToken);

        return new NodeDetailDto(node.ToDto(now, isGateway, registered.Contains(node.NodeNum)), traceroute?.ToDto(now), heardBy);
    }

    private async Task<IReadOnlyList<HeardByDto>> HeardByAsync(uint nodeNum, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var receptions = await stores.Receptions.GetForNodeAsync(nodeNum, cancellationToken);
        var gatewayNums = receptions.Select(reception => reception.GatewayNodeNum).Distinct().ToList();
        var online = (await stores.Gateways.GetActiveByNodeNumsAsync(gatewayNums, cancellationToken))
            .Where(gateway => gateway.CanSend)
            .Select(gateway => gateway.NodeNum!.Value)
            .ToHashSet();
        var names = (await stores.Nodes.GetManyAsync(gatewayNums, cancellationToken)).ToDictionary(gateway => gateway.NodeNum, gateway => gateway.LongName);

        // The gateway we would route through comes first; the rest by most recent.
        var best = GatewayRouting.Pick(receptions, online, now);
        return [.. receptions
            .OrderByDescending(reception => reception == best)
            .ThenByDescending(reception => reception.LastHeardAt)
            .Select(reception => new HeardByDto(
                reception.GatewayNodeNum,
                MeshNode.FormatNodeId(reception.GatewayNodeNum),
                names.GetValueOrDefault(reception.GatewayNodeNum) ?? MeshNode.FormatNodeId(reception.GatewayNodeNum),
                online.Contains(reception.GatewayNodeNum),
                reception.LastHeardAt,
                reception.Snr,
                reception.Rssi,
                reception.HopsAway))];
    }
}
