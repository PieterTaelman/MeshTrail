using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodeById;

public sealed class GetNodeByIdHandler(
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    INodeTracerouteRepository traceroutes,
    INodeRegistrationRepository registrations,
    TimeProvider timeProvider) : IQueryHandler<GetNodeByIdQuery, NodeDetailDto>
{
    public async ValueTask<NodeDetailDto> Handle(GetNodeByIdQuery query, CancellationToken cancellationToken)
    {
        var node = await nodes.GetAsync(query.NodeNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Node {MeshNode.FormatNodeId(query.NodeNum)} is not known.");

        var now = timeProvider.GetUtcNow();
        var gateway = await gateways.GetAsync(MeshGateway.PrimaryKey, cancellationToken);
        var traceroute = await traceroutes.GetLatestForNodeAsync(query.NodeNum, cancellationToken);
        var registered = await registrations.GetVerifiedNodeNumsAsync([node.NodeNum], cancellationToken);
        return new NodeDetailDto(node.ToDto(now, gateway?.NodeNum, registered.Contains(node.NodeNum)), traceroute?.ToDto(now));
    }
}
