using Mediator;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodeById;

public sealed record GetNodeByIdQuery(uint NodeNum) : IQuery<NodeDetailDto>;
