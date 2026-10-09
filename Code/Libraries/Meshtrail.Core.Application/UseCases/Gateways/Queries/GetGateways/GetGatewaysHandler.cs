using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Queries.GetGateways;

public sealed class GetGatewaysHandler(IMeshGatewayRepository gateways, IMeshNodeRepository nodes, ICurrentUser currentUser)
    : IQueryHandler<GetGatewaysQuery, IReadOnlyList<GatewayDto>>
{
    public async ValueTask<IReadOnlyList<GatewayDto>> Handle(GetGatewaysQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.StableId();
        var list = query.Mine
            ? await gateways.GetActiveForOwnerAsync(userId, cancellationToken)
            : await gateways.GetBoundAsync(cancellationToken);
        return await GatewayViews.ToDtosAsync(list, nodes, gateways, userId, cancellationToken);
    }
}
