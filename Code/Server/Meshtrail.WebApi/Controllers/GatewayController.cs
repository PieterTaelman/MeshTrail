using Asp.Versioning;
using Mediator;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.ReconnectGateway;
using Meshtrail.Core.Application.UseCases.Mesh.Queries.GetGatewayStatus;
using Meshtrail.Core.Contracts.Mesh;
using Microsoft.AspNetCore.Mvc;

namespace Meshtrail.WebApi.Controllers;

/// <summary>Status of the Meshtastic gateway connection.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/gateway")]
public sealed class GatewayController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GatewayStatusDto>> Get(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetGatewayStatusQuery(), cancellationToken));

    /// <summary>Drops the connection and connects again right away. The new status arrives via SignalR.</summary>
    [HttpPost("reconnect")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Reconnect(CancellationToken cancellationToken)
    {
        await sender.Send(new ReconnectGatewayCommand(), cancellationToken);
        return Accepted();
    }
}
