using Asp.Versioning;
using Mediator;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestPosition;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestTraceroute;
using Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodeById;
using Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodes;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;
using Microsoft.AspNetCore.Mvc;

namespace Meshtrail.WebApi.Controllers;

/// <summary>Mesh nodes the gateway has discovered. Node numbers are the uint32 numbers the radio uses.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/nodes")]
public sealed class NodesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<NodeDto>>> GetList([FromQuery] NodeListRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetNodesQuery(request), cancellationToken));

    [HttpGet("{nodeNum}")]
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
