using System.Diagnostics;
using System.Security.Claims;
using Meshtrail.Core.Application.Abstractions;

namespace Meshtrail.WebApi.Infrastructure;

/// <summary>
/// Reads the signed-in user from the HTTP request. Outside a request (background jobs) it reports "system".
/// </summary>
internal sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private const string SystemUser = "system";

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public string? Id => Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal?.FindFirstValue("sub");

    public string Name =>
        Principal?.Identity?.Name
        ?? Principal?.FindFirstValue("preferred_username")
        ?? Id
        ?? SystemUser;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string CorrelationId =>
        Activity.Current?.TraceId.ToString()
        ?? httpContextAccessor.HttpContext?.TraceIdentifier
        ?? string.Empty;
}
