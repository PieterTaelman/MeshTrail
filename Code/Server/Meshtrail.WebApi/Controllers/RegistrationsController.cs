using Asp.Versioning;
using Mediator;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RegisterFromContactUrl;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RevokeRegistration;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.VerifyRegistration;
using Meshtrail.Core.Application.UseCases.Mesh.Queries.GetMyRegistrations;
using Meshtrail.Core.Contracts.Mesh;
using Microsoft.AspNetCore.Mvc;

namespace Meshtrail.WebApi.Controllers;

/// <summary>Registering mesh nodes to the signed-in user: claim with the contact link, prove it with the code.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/registrations")]
public sealed class RegistrationsController(ISender sender) : ControllerBase
{
    /// <summary>The current user's claimed and verified registrations.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RegistrationDto>>> GetMine(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMyRegistrationsQuery(), cancellationToken));

    /// <summary>Claims the node in the link and sends it a 6-digit code by direct message.</summary>
    [HttpPost("from-contact-url")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<RegistrationDto>> RegisterFromContactUrl(RegisterFromContactUrlRequest request, CancellationToken cancellationToken)
    {
        var registration = await sender.Send(new RegisterFromContactUrlCommand(request.Url), cancellationToken);
        return CreatedAtAction(nameof(GetMine), new { version = "1" }, registration);
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<ActionResult<RegistrationDto>> Verify(Guid id, VerifyRegistrationRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new VerifyRegistrationCommand(id, request.Code), cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new RevokeRegistrationCommand(id), cancellationToken);
        return NoContent();
    }
}
