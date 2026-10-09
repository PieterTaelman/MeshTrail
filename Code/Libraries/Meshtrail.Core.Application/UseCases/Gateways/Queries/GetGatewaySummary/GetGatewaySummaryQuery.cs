using Mediator;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Queries.GetGatewaySummary;

public sealed record GetGatewaySummaryQuery : IQuery<GatewaySummaryDto>;
