using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetMessages;

/// <summary>
/// A team chat (members only; 404 for others) or a direct-message conversation (only for the node's owner and the
/// people who wrote to it; others get an empty page, the same as a conversation that has not started).
/// </summary>
public sealed class GetMessagesHandler(
    IMeshMessageRepository messages,
    ITeamRepository teams,
    MessageAudience audience,
    ICurrentUser currentUser) : IQueryHandler<GetMessagesQuery, PagedResult<MessageDto>>
{
    public async ValueTask<PagedResult<MessageDto>> Handle(GetMessagesQuery query, CancellationToken cancellationToken)
    {
        var request = query.Request;
        var userId = currentUser.StableId();

        if (request.Team is { } teamId)
        {
            var team = await teams.GetAsync(teamId, cancellationToken);
            if (team is null || !team.IsMember(userId))
            {
                throw new KeyNotFoundException($"Team {teamId} does not exist.");
            }
        }
        else if (!(await audience.ForConversationAsync(request.Node!.Value, cancellationToken)).Contains(userId))
        {
            return new PagedResult<MessageDto>([], 0, request.Page, request.PageSize);
        }

        var page = await messages.GetPageAsync(request, cancellationToken);
        return new PagedResult<MessageDto>([.. page.Items.Select(message => message.ToDto())], page.TotalCount, page.Page, page.PageSize);
    }
}
