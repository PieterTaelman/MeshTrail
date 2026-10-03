using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodeById;

public sealed class GetNodeByIdHandler(
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    INodeTracerouteRepository traceroutes,
    TimeProvider timeProvider) : IQueryHandler<GetNodeByIdQuery, NodeDetailDto>
{
    public async ValueTask<NodeDetailDto> Handle(GetNodeByIdQuery query, CancellationToken cancellationToken)
    {
        var node = await nodes.GetAsync(query.NodeNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Node {MeshNode.FormatNodeId(query.NodeNum)} is not known.");

        var now = timeProvider.GetUtcNow();
        var gateway = await gateways.GetAsync(MeshGateway.PrimaryKey, cancellationToken);
        var traceroute = await traceroutes.GetLatestForNodeAsync(query.NodeNum, cancellationToken);
        return new NodeDetailDto(node.ToDto(now, gateway?.NodeNum), traceroute?.ToDto(now));
    }
}
