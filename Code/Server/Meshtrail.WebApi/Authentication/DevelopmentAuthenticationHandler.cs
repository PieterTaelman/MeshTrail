using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Meshtrail.WebApi.Authentication;

/// <summary>
/// Signs every request in as one configurable user (Authentication:DevelopmentUser), so local runs and integration
/// tests work before an identity provider exists. An X-Dev-User header picks another user, so tests can act as
/// several people. Never enabled outside Development/Testing (see <see cref="AuthenticationSetup"/>).
/// </summary>
internal sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";
    public const string UserHeader = "X-Dev-User";

    /// <summary>Header values longer than this are ignored (the name ends up in audit columns).</summary>
    private const int MaxUserLength = 64;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var requested = Request.Headers[UserHeader].ToString().Trim();
        var userName = requested is { Length: > 0 and <= MaxUserLength }
            ? requested
            : configuration["Authentication:DevelopmentUser"] ?? "developer";
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, userName),
            new(ClaimTypes.Name, userName),
        ];

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
