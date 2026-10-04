using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodes;

public sealed class GetNodesHandler(
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    INodeRegistrationRepository registrations,
    TimeProvider timeProvider)
    : IQueryHandler<GetNodesQuery, PagedResult<NodeDto>>
{
    public async ValueTask<PagedResult<NodeDto>> Handle(GetNodesQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var page = await nodes.GetPageAsync(query.Request, now - MeshNode.OnlineWindow, cancellationToken);
        var gateway = await gateways.GetAsync(MeshGateway.PrimaryKey, cancellationToken);
        var registered = await registrations.GetVerifiedNodeNumsAsync([.. page.Items.Select(node => node.NodeNum)], cancellationToken);

        return new PagedResult<NodeDto>(
            [.. page.Items.Select(node => node.ToDto(now, gateway?.NodeNum, registered.Contains(node.NodeNum)))],
            page.TotalCount,
            page.Page,
            page.PageSize);
    }
}
