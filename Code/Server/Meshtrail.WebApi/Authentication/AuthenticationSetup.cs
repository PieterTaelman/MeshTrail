using System.Security.Claims;
using Meshtrail.Core.Infrastructure.Accounts;
using Meshtrail.WebApi.Realtime;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Meshtrail.WebApi.Authentication;

/// <summary>
/// Authentication modes (Authentication:Mode):
/// <list type="bullet">
/// <item><b>Local</b> (default): Meshtrail's own accounts; the API validates the access tokens it signed
/// (Authentication:Local).</item>
/// <item><b>Development</b> (Development/Testing only): a request with a Bearer token is checked like Local; any other
/// request is signed in as a fixed user (or the X-Dev-User header), so tests can act as several people.</item>
/// </list>
/// </summary>
internal static class AuthenticationSetup
{
    public const string ModeKey = "Authentication:Mode";
    public const string DevelopmentMode = "Development";
    public const string LocalMode = "Local";
    private const string DevelopmentOrBearerScheme = "DevelopmentOrBearer";

    public static IServiceCollection AddMeshtrailAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var mode = configuration[ModeKey] ?? LocalMode;
        var local = configuration.GetSection(LocalAuthOptions.SectionName).Get<LocalAuthOptions>() ?? new LocalAuthOptions();

        if (string.Equals(mode, DevelopmentMode, StringComparison.OrdinalIgnoreCase))
        {
            // Safety net: the fake login must never be switched on in a real environment.
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException(
                    $"{ModeKey}={DevelopmentMode} is only allowed in the Development and Testing environments.");
            }

            services.AddAuthentication(DevelopmentOrBearerScheme)
                .AddPolicyScheme(DevelopmentOrBearerScheme, null, options => options.ForwardDefaultSelector = context =>
                    context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? JwtBearerDefaults.AuthenticationScheme
                        : DevelopmentAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.SchemeName, null)
                .AddJwtBearer(options => ConfigureLocalBearer(options, local));
        }
        else
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options => ConfigureLocalBearer(options, local));
        }

        // SignalR sends to "users" by this id (Clients.Users), so it must be the same id handlers use.
        services.AddSingleton<IUserIdProvider, StableUserIdProvider>();

        // Every endpoint needs a signed-in user unless it opts out with AllowAnonymous (the public map, account pages,
        // health, API docs).
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    private static void ConfigureLocalBearer(JwtBearerOptions options, LocalAuthOptions local)
    {
        // Keep the claim names as they are in the token ("sub", "name"): no renaming to long URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = local.Issuer,
            ValidAudience = local.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(local.SigningKeyBytes()),
            NameClaimType = JwtRegisteredClaimNames.Name,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        // Browsers cannot send headers on WebSockets, so SignalR passes the token in the query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(NotificationsHub.Path))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
        };
    }

    /// <summary>The user id SignalR groups connections by: the account id ("sub"), or the development user's name.</summary>
    private sealed class StableUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection) =>
            connection.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? connection.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? connection.User.Identity?.Name;
    }
}
