using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Meshtrail.WebApi.Infrastructure;

/// <summary>Rate limits: the account endpoints are an easy target for password guessing and mail spam.</summary>
internal static class RateLimits
{
    public const string Account = "account";

    /// <summary>RateLimiting:AccountPerMinute requests per client address per minute (default 30).</summary>
    public static IServiceCollection AddMeshtrailRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var perMinute = configuration.GetValue("RateLimiting:AccountPerMinute", 30);
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Account, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }
}
