using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetMessages;

public sealed class GetMessagesHandler(IMeshMessageRepository messages) : IQueryHandler<GetMessagesQuery, PagedResult<MessageDto>>
{
    public async ValueTask<PagedResult<MessageDto>> Handle(GetMessagesQuery query, CancellationToken cancellationToken)
    {
        var page = await messages.GetPageAsync(query.Request, cancellationToken);
        return new PagedResult<MessageDto>([.. page.Items.Select(message => message.ToDto())], page.TotalCount, page.Page, page.PageSize);
    }
}
