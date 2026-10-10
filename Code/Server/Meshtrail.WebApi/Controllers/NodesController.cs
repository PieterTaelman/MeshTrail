using Asp.Versioning;
using Mediator;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestPosition;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestTraceroute;
using Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodeById;
using Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodes;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meshtrail.WebApi.Controllers;

/// <summary>
/// Mesh nodes any gateway has discovered, worldwide. Node numbers are the uint32 numbers the radio uses. Asking a
/// node for something goes through the gateway that heard it best (422 when none can reach it).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/nodes")]
public sealed class NodesController(ISender sender) : ControllerBase
{
    /// <summary>Nodes in the map view (?bbox=west,south,east,north) and/or matching ?search=, most recently heard first.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<NodeDto>>> GetList([FromQuery] NodeListRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetNodesQuery(request), cancellationToken));

    [HttpGet("{nodeNum}")]
    [AllowAnonymous]
    public async Task<ActionResult<NodeDetailDto>> GetById(uint nodeNum, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetNodeByIdQuery(nodeNum), cancellationToken));

    /// <summary>Asks the node for a fresh position. The answer arrives later via SignalR (NodeUpdated).</summary>
    [HttpPost("{nodeNum}/position-request")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RequestPosition(uint nodeNum, CancellationToken cancellationToken)
    {
        await sender.Send(new RequestPositionCommand(nodeNum), cancellationToken);
        return Accepted();
    }

    /// <summary>Starts a traceroute. Returns it as Pending; the result arrives via SignalR (TracerouteCompleted).</summary>
    [HttpPost("{nodeNum}/traceroute")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<NodeTracerouteDto>> RequestTraceroute(uint nodeNum, CancellationToken cancellationToken) =>
        Accepted(await sender.Send(new RequestTracerouteCommand(nodeNum), cancellationToken));
}
