using Asp.Versioning;
using Mediator;
using Meshtrail.Core.Application.UseCases.Gateways.Commands.AddGateway;
using Meshtrail.Core.Application.UseCases.Gateways.Commands.RevokeGateway;
using Meshtrail.Core.Application.UseCases.Gateways.Queries.GetGateways;
using Meshtrail.Core.Application.UseCases.Gateways.Queries.GetGatewaySummary;
using Meshtrail.Core.Contracts.Mesh;
using Microsoft.AspNetCore.Mvc;

namespace Meshtrail.WebApi.Controllers;

/// <summary>Gateways: nodes that connect the mesh around them to Meshtrail. Anyone can add their own node as a gateway.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/gateways")]
public sealed class GatewaysController(ISender sender) : ControllerBase
{
    /// <summary>?mine=true: your gateways (pending ones included). Otherwise every active gateway.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GatewayDto>>> GetList([FromQuery] bool mine, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetGatewaysQuery(mine), cancellationToken));

    /// <summary>Online and total number of gateways (the top-bar chip).</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<GatewaySummaryDto>> GetSummary(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetGatewaySummaryQuery(), cancellationToken));

    /// <summary>
    /// Creates MQTT credentials for a new gateway. The password is in this answer only. The gateway stays Pending until
    /// the first uplink with these credentials, which ties it to that node.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<GatewayCredentialsDto>> Add(CancellationToken cancellationToken)
    {
        var credentials = await sender.Send(new AddGatewayCommand(), cancellationToken);
        return CreatedAtAction(nameof(GetList), new { version = "1", mine = true }, credentials);
    }

    /// <summary>Removes one of your gateways: its login stops working.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new RevokeGatewayCommand(id), cancellationToken);
        return NoContent();
    }
}
