using Mediator;
using Meshtrail.Core.Contracts.Teams;

namespace Meshtrail.Core.Application.UseCases.Teams.Commands.JoinTeam;

/// <summary>Joins the team with this join code (joining twice is fine).</summary>
public sealed record JoinTeamCommand(string Code) : ICommand<TeamDto>;
