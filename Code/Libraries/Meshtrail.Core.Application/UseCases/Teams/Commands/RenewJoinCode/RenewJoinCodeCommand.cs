using Mediator;
using Meshtrail.Core.Contracts.Teams;

namespace Meshtrail.Core.Application.UseCases.Teams.Commands.RenewJoinCode;

/// <summary>The owner replaces the join code (the old one stops working).</summary>
public sealed record RenewJoinCodeCommand(Guid Id) : ICommand<TeamDto>;
