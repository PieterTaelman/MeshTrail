using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Contracts.Teams;

namespace Meshtrail.Core.Application.UseCases.Teams.Queries.GetMyTeams;

public sealed class GetMyTeamsHandler(
    ITeamRepository teams,
    IMeshGatewayRepository gateways,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IQueryHandler<GetMyTeamsQuery, IReadOnlyList<TeamDto>>
{
    public async ValueTask<IReadOnlyList<TeamDto>> Handle(GetMyTeamsQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.StableId();
        var now = timeProvider.GetUtcNow();
        var result = new List<TeamDto>();
        foreach (var team in await teams.GetForUserAsync(userId, cancellationToken))
        {
            result.Add(await team.ToDtoAsync(userId, gateways, now, cancellationToken));
        }

        return result;
    }
}
