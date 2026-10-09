using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Contracts.Teams;

namespace Meshtrail.Core.Application.UseCases.Teams.Commands.JoinTeam;

public sealed class JoinTeamHandler(
    ITeamRepository teams,
    IMeshGatewayRepository gateways,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<JoinTeamCommand, TeamDto>
{
    public async ValueTask<TeamDto> Handle(JoinTeamCommand command, CancellationToken cancellationToken)
    {
        var team = await teams.GetByJoinCodeAsync(command.Code.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new KeyNotFoundException("No team has this join code.");

        var now = timeProvider.GetUtcNow();
        var userId = currentUser.StableId();
        if (team.Join(userId, currentUser.Name, now))
        {
            await teams.UpdateAsync(team, cancellationToken);
            await teams.SaveChangesAsync(cancellationToken);
        }

        return await team.ToDtoAsync(userId, gateways, now, cancellationToken);
    }
}
