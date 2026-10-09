using Asp.Versioning;
using Mediator;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.SendMessage;
using Meshtrail.Core.Application.UseCases.Mesh.Queries.GetMessages;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;
using Microsoft.AspNetCore.Mvc;

namespace Meshtrail.WebApi.Controllers;

/// <summary>
/// Direct messages (through the gateway that heard the node best) and team chat (through every gateway that carries
/// the team's channel). Only the people involved can read them.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/messages")]
public sealed class MessagesController(ISender sender) : ControllerBase
{
    /// <summary>One conversation (?node=4044729068) or one team chat (?team=…), newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<MessageDto>>> GetList([FromQuery] MessageListRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMessagesQuery(request), cancellationToken));

    /// <summary>
    /// Queues the message (202), or 422 when no online gateway heard the node recently. Status updates (Sent, Acked,
    /// Failed) arrive via SignalR (MessageStatusChanged).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<MessageDto>> Send(SendMessageRequest request, CancellationToken cancellationToken) =>
        Accepted(await sender.Send(new SendMessageCommand(request.ToNodeNum, request.TeamId, request.Text), cancellationToken));
}
