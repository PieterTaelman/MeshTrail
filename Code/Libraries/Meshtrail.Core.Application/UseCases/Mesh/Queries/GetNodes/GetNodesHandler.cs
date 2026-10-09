using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodes;

/// <summary>Nodes in the map view and/or matching a search, anywhere in the world; filtered and paged in SQL.</summary>
public sealed class GetNodesHandler(
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    INodeRegistrationRepository registrations,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : IQueryHandler<GetNodesQuery, PagedResult<NodeDto>>
{
    public async ValueTask<PagedResult<NodeDto>> Handle(GetNodesQuery query, CancellationToken cancellationToken)
    {
        var request = query.Request;
        var now = timeProvider.GetUtcNow();
        var filter = new NodeFilter(
            request.Search?.Trim() is { Length: > 0 } search ? search : null,
            request.Registered,
            request.Online,
            BoundingBox.TryParse(request.Bbox, out var box) ? box : null,
            request.Owner is null ? null : currentUser.StableId(),
            now - MeshNode.OnlineWindow);
        var page = await nodes.GetPageAsync(filter, request.Page, request.PageSize, cancellationToken);

        var nodeNums = page.Items.Select(node => node.NodeNum).ToList();
        var registered = await registrations.GetVerifiedNodeNumsAsync(nodeNums, cancellationToken);
        var gatewayNodes = (await gateways.GetActiveByNodeNumsAsync(nodeNums, cancellationToken)).Select(gateway => gateway.NodeNum!.Value).ToHashSet();

        return new PagedResult<NodeDto>(
            [.. page.Items.Select(node => node.ToDto(now, gatewayNodes.Contains(node.NodeNum), registered.Contains(node.NodeNum)))],
            page.TotalCount,
            page.Page,
            page.PageSize);
    }
}
