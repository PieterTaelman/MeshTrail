using Mediator;
using Meshtrail.Core.Contracts.Teams;

namespace Meshtrail.Core.Application.UseCases.Teams.Queries.GetMyTeams;

/// <summary>The teams the current user is in.</summary>
public sealed record GetMyTeamsQuery : IQuery<IReadOnlyList<TeamDto>>;
