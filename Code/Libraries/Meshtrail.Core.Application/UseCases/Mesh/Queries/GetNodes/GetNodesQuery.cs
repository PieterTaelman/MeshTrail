using Mediator;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodes;

public sealed record GetNodesQuery(NodeListRequest Request) : IQuery<PagedResult<NodeDto>>;
