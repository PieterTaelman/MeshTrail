using Asp.Versioning;
using Mediator;
using Meshtrail.Core.Application.UseCases.Teams.Commands.CreateTeam;
using Meshtrail.Core.Application.UseCases.Teams.Commands.JoinTeam;
using Meshtrail.Core.Application.UseCases.Teams.Commands.LeaveTeam;
using Meshtrail.Core.Application.UseCases.Teams.Commands.RenewJoinCode;
using Meshtrail.Core.Application.UseCases.Teams.Queries.GetMyTeams;
using Meshtrail.Core.Contracts.Teams;
using Microsoft.AspNetCore.Mvc;

namespace Meshtrail.WebApi.Controllers;

/// <summary>Teams: people who chat on their own Meshtastic channel. Join with a join code; chat via GET/POST messages?team=.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teams")]
public sealed class TeamsController(ISender sender) : ControllerBase
{
    /// <summary>The teams you are in.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> GetMine(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMyTeamsQuery(), cancellationToken));

    /// <summary>Creates a team on its own channel; you become its owner. 422 for a public or taken channel name.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<TeamDto>> Create(CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var team = await sender.Send(new CreateTeamCommand(request.Name, request.ChannelName), cancellationToken);
        return CreatedAtAction(nameof(GetMine), new { version = "1" }, team);
    }

    /// <summary>Joins the team with this code (404 for an unknown code).</summary>
    [HttpPost("join")]
    public async Task<ActionResult<TeamDto>> Join(JoinTeamRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new JoinTeamCommand(request.Code), cancellationToken));

    /// <summary>The owner replaces the join code.</summary>
    [HttpPost("{id:guid}/join-code")]
    public async Task<ActionResult<TeamDto>> RenewJoinCode(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new RenewJoinCodeCommand(id), cancellationToken));

    /// <summary>You leave the team (the last member leaving ends it).</summary>
    [HttpDelete("{id:guid}/members/me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Leave(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new LeaveTeamCommand(id), cancellationToken);
        return NoContent();
    }
}
