using Mediator;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetGatewayStatus;

public sealed record GetGatewayStatusQuery : IQuery<GatewayStatusDto>;
