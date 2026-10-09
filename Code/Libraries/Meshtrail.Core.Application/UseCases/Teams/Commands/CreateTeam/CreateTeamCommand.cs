using Mediator;
using Meshtrail.Core.Contracts.Teams;

namespace Meshtrail.Core.Application.UseCases.Teams.Commands.CreateTeam;

/// <summary>Creates a team on its own Meshtastic channel; the current user becomes its owner.</summary>
public sealed record CreateTeamCommand(string Name, string ChannelName) : ICommand<TeamDto>;
