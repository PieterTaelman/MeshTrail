using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Contracts.Teams;

namespace Meshtrail.Core.Application.UseCases.Teams.Commands.RenewJoinCode;

public sealed class RenewJoinCodeHandler(
    ITeamRepository teams,
    IMeshGatewayRepository gateways,
    ITeamJoinCodeGenerator codes,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<RenewJoinCodeCommand, TeamDto>
{
    public async ValueTask<TeamDto> Handle(RenewJoinCodeCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.StableId();
        var team = await teams.GetAsync(command.Id, cancellationToken);
        if (team is null || !team.IsMember(userId))
        {
            throw new KeyNotFoundException($"Team {command.Id} does not exist.");
        }

        team.RenewJoinCode(userId, codes.NewCode());
        await teams.UpdateAsync(team, cancellationToken);
        await teams.SaveChangesAsync(cancellationToken);
        return await team.ToDtoAsync(userId, gateways, timeProvider.GetUtcNow(), cancellationToken);
    }
}
