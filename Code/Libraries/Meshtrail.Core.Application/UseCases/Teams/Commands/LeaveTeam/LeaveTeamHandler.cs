using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;

namespace Meshtrail.Core.Application.UseCases.Teams.Commands.LeaveTeam;

public sealed class LeaveTeamHandler(ITeamRepository teams, ICurrentUser currentUser) : ICommandHandler<LeaveTeamCommand>
{
    public async ValueTask<Unit> Handle(LeaveTeamCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.StableId();
        var team = await teams.GetAsync(command.Id, cancellationToken);
        if (team is null || !team.IsMember(userId))
        {
            throw new KeyNotFoundException($"Team {command.Id} does not exist.");
        }

        if (team.Leave(userId))
        {
            await teams.DeleteAsync(team, cancellationToken);
        }
        else
        {
            await teams.UpdateAsync(team, cancellationToken);
        }

        await teams.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
