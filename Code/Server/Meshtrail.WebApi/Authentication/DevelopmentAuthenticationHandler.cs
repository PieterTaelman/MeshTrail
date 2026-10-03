using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Meshtrail.WebApi.Authentication;

/// <summary>
/// Signs every request in as one configurable user (Authentication:DevelopmentUser), so local runs and
/// integration tests work before an identity provider exists. Never enabled outside Development/Testing.
/// </summary>
internal sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userName = configuration["Authentication:DevelopmentUser"] ?? "developer";
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, userName),
            new(ClaimTypes.Name, userName),
        ];

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
