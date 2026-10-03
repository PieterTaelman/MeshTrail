using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Meshtrail.WebApi.Realtime;

namespace Meshtrail.WebApi.Authentication;

/// <summary>
/// Authentication skeleton. "JwtBearer" (default) validates tokens from the identity provider, which is not chosen yet;
/// configure it under Authentication:Schemes:Bearer. "Development" signs everyone in as a fixed user, for local runs and tests only.
/// </summary>
internal static class AuthenticationSetup
{
    public const string ModeKey = "Authentication:Mode";
    public const string DevelopmentMode = "Development";
    public const string JwtBearerMode = "JwtBearer";

    public static IServiceCollection AddMeshtrailAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var mode = configuration[ModeKey] ?? JwtBearerMode;

        if (string.Equals(mode, DevelopmentMode, StringComparison.OrdinalIgnoreCase))
        {
            // Safety net: the fake login must never be switched on in a real environment.
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException(
                    $"{ModeKey}={DevelopmentMode} is only allowed in the Development and Testing environments.");
            }

            services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.SchemeName, null);
        }
        else
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
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
                });
        }

        // Every endpoint needs a signed-in user unless it opts out with AllowAnonymous (health, API docs).
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
