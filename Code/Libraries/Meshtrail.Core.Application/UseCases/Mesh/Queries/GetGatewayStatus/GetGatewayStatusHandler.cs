using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetGatewayStatus;

public sealed class GetGatewayStatusHandler(IMeshGatewayRepository gateways, TimeProvider timeProvider)
    : IQueryHandler<GetGatewayStatusQuery, GatewayStatusDto>
{
    public async ValueTask<GatewayStatusDto> Handle(GetGatewayStatusQuery query, CancellationToken cancellationToken)
    {
        var gateway = await gateways.GetAsync(MeshGateway.PrimaryKey, cancellationToken);

        // No row yet = the worker has not started; report that instead of a 404 so the top bar can always show a chip.
        return gateway?.ToDto() ?? new GatewayStatusDto(
            GatewayStatus.Offline.ToString(), string.Empty, timeProvider.GetUtcNow(), null, "The gateway has not started yet.", null, null, null);
    }
}
