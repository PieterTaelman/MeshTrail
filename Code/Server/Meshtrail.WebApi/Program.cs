using Asp.Versioning;
using Mediator;
using Meshtrail.Core.Application;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Behaviors;
using Meshtrail.Core.Infrastructure;
using Meshtrail.Core.Infrastructure.Mesh;
using Meshtrail.Core.Infrastructure.Persistence;
using Meshtrail.WebApi.Authentication;
using Meshtrail.WebApi.Infrastructure;
using Meshtrail.WebApi.Realtime;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, resilience and service discovery (replaces SysLib web hosting).
builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    // The mesh platform (gateway transports, ingest, outbox) runs inside this process (see Documentation/Mesh/README.md).
    .AddMesh(builder.Configuration);

// The source generator writes AddMediator at compile time from this lambda, so keep it a plain literal.
builder.Services.AddMediator((MediatorOptions options) =>
{
    // Scoped because handlers use scoped repositories and the DbContext.
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.Assemblies = [typeof(ApplicationAssemblyMarker)];
    // Order matters: validation runs first so invalid messages never reach logging or the handler.
    options.PipelineBehaviors = [typeof(ValidationBehavior<,>), typeof(LoggingBehavior<,>)];
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddMeshtrailAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddMeshtrailRateLimits(builder.Configuration);

builder.Services.AddControllers();
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        // The version is always in the URL (api/v1/...), so only read it from there.
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
        options.ReportApiVersions = true;
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        // Group "v1" matches the OpenAPI document name below, and {version} in routes becomes "1".
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    })
    // One OpenAPI document per API version (/openapi/v1.json), shown in Scalar at /scalar.
    .AddOpenApi();

builder.Services.AddSignalR();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<MeshtrailDbContext>("database")
    // Reports Degraded (never Unhealthy) while a gateway transport is offline, so /health/ready stays 200.
    .AddCheck<MeshGatewayHealthCheck>(MeshGatewayHealthCheck.Name);

const string CorsPolicy = "client-web";
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    // SignalR needs credentials (cookies / auth header) on its negotiate call.
    .AllowCredentials()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi().WithDocumentPerVersion().AllowAnonymous();
app.MapScalarApiReference().AllowAnonymous();
app.MapDefaultEndpoints();
app.MapControllers();
app.MapHub<NotificationsHub>(NotificationsHub.Path);

app.Run();

/// <summary>Public so WebApplicationFactory in the integration tests can start the app.</summary>
public partial class Program;
