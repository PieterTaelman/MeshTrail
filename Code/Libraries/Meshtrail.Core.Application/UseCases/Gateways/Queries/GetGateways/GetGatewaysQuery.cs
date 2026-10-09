using Mediator;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Queries.GetGateways;

/// <summary>Mine = true: the current user's gateways (pending ones included). Otherwise every active, bound gateway.</summary>
public sealed record GetGatewaysQuery(bool Mine) : IQuery<IReadOnlyList<GatewayDto>>;
