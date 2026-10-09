using Mediator;

namespace Meshtrail.Core.Application.UseCases.Teams.Commands.LeaveTeam;

/// <summary>The current user leaves the team. The last member leaving ends the team.</summary>
public sealed record LeaveTeamCommand(Guid Id) : ICommand;
