using Asp.Versioning;
using Mediator;
using Meshtrail.Core.Application.UseCases.Gateways.Queries.AuthenticateGateway;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.WebApi.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meshtrail.WebApi.Controllers;

/// <summary>
/// Internal: the MQTT broker asks whether a gateway login is valid. Not for browsers: it needs the service key
/// (the broker's service password) instead of a user login.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/mqtt")]
[AllowAnonymous]
[RequireServiceKey]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class MqttAuthController(ISender sender) : ControllerBase
{
    [HttpPost("auth")]
    public async Task<ActionResult<AuthenticateGatewayResponse>> Authenticate(AuthenticateGatewayRequest request, CancellationToken cancellationToken) =>
        Ok(new AuthenticateGatewayResponse(await sender.Send(new AuthenticateGatewayQuery(request.UserName, request.Password), cancellationToken)));
}
