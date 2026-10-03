using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Shared plumbing every Meshtrail service gets: OpenTelemetry, health checks, HTTP resilience and service discovery.
/// Replaces what SysLib.Application.WebHosting / SysLib.Application.OpenTelemetry did in CRT.
/// </summary>
public static class Extensions
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    /// <summary>Tag for checks that only say "the process is running" (no database, no network).</summary>
    public const string LiveTag = "live";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Retries, timeouts and a circuit breaker for every outgoing HttpClient call.
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Meshtrail.*"))
            .WithTracing(tracing => tracing
                // Every ActivitySource named "Meshtrail.*" (one per layer) is exported automatically.
                .AddSource("Meshtrail.*")
                .AddAspNetCoreInstrumentation(options =>
                    // Health probes run every few seconds; tracing them only adds noise.
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments(LivePath) &&
                        !context.Request.Path.StartsWithSegments(ReadyPath))
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation());

        builder.AddOpenTelemetryExporters();
        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);
        return builder;
    }

    /// <summary>
    /// /health/live = process is up (only "live" checks). /health/ready = all checks, incl. the database.
    /// Mapped in every environment because on-prem monitoring needs them too.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(LivePath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(LiveTag),
        }).AllowAnonymous();

        app.MapHealthChecks(ReadyPath).AllowAnonymous();
        return app;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Aspire sets this locally (dashboard). On-prem the sink is not chosen yet; set the variable to enable it.
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
}
