using Mediator;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetMessages;

public sealed record GetMessagesQuery(MessageListRequest Request) : IQuery<PagedResult<MessageDto>>;
