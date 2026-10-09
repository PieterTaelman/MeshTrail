using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Queries.GetGatewaySummary;

public sealed class GetGatewaySummaryHandler(IMeshGatewayRepository gateways) : IQueryHandler<GetGatewaySummaryQuery, GatewaySummaryDto>
{
    public async ValueTask<GatewaySummaryDto> Handle(GetGatewaySummaryQuery query, CancellationToken cancellationToken)
    {
        var (online, total) = await gateways.CountAsync(cancellationToken);
        return new GatewaySummaryDto(online, total);
    }
}
