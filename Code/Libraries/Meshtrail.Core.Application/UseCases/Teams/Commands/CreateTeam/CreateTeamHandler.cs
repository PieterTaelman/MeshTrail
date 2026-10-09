using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Contracts.Teams;
using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Teams;

namespace Meshtrail.Core.Application.UseCases.Teams.Commands.CreateTeam;

/// <summary>Check the channel name is free → create the team with the current user as owner.</summary>
public sealed class CreateTeamHandler(
    ITeamRepository teams,
    IMeshGatewayRepository gateways,
    ITeamJoinCodeGenerator codes,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<CreateTeamCommand, TeamDto>
{
    public async ValueTask<TeamDto> Handle(CreateTeamCommand command, CancellationToken cancellationToken)
    {
        var channel = Team.CheckChannelName(command.ChannelName);
        if (await teams.ChannelNameTakenAsync(channel, cancellationToken))
        {
            throw new DomainException($"Another team already uses the channel \"{channel}\". Pick another channel name.");
        }

        var now = timeProvider.GetUtcNow();
        var userId = currentUser.StableId();
        var team = Team.Create(command.Name, channel, codes.NewCode(), userId, currentUser.Name, now);
        await teams.AddAsync(team, cancellationToken);
        await teams.SaveChangesAsync(cancellationToken);
        return await team.ToDtoAsync(userId, gateways, now, cancellationToken);
    }
}
