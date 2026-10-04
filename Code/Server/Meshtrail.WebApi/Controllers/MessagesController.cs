using Asp.Versioning;
using Mediator;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.SendMessage;
using Meshtrail.Core.Application.UseCases.Mesh.Queries.GetMessages;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;
using Microsoft.AspNetCore.Mvc;

namespace Meshtrail.WebApi.Controllers;

/// <summary>Chat over the mesh: channel broadcasts and direct messages.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/messages")]
public sealed class MessagesController(ISender sender) : ControllerBase
{
    /// <summary>One channel (?channel=0) or one conversation (?node=4044729068), newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<MessageDto>>> GetList([FromQuery] MessageListRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMessagesQuery(request), cancellationToken));

    /// <summary>Queues the message (202). Status updates (Sent, Acked, Failed) arrive via SignalR (MessageStatusChanged).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<MessageDto>> Send(SendMessageRequest request, CancellationToken cancellationToken) =>
        Accepted(await sender.Send(new SendMessageCommand(request.ChannelIndex, request.ToNodeNum, request.Text), cancellationToken));
}
