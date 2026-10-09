using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Infrastructure.Jobs;
using Meshtrail.Core.Infrastructure.Mesh;
using Meshtrail.Core.Infrastructure.Repositories;
using Meshtrail.Core.Infrastructure.Persistence;
using Meshtrail.Mesh.Radio;
using Meshtrail.Mesh.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meshtrail.Core.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "MeshtrailDatabase";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Aspire injects ConnectionStrings__MeshtrailDatabase locally; on-prem it comes from appsettings/env.
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        services.AddDbContext<MeshtrailDbContext>(options => options.UseSqlServer(
            connectionString,
            sql => sql.EnableRetryOnFailure()));

        services.AddScoped<ISampleRepository, SampleRepository>();
        services.AddScoped<IMeshNodeRepository, MeshNodeRepository>();
        services.AddScoped<IMeshGatewayRepository, MeshGatewayRepository>();
        services.AddScoped<INodeReceptionRepository, NodeReceptionRepository>();
        services.AddScoped<INodeTracerouteRepository, NodeTracerouteRepository>();
        services.AddScoped<INodeRegistrationRepository, NodeRegistrationRepository>();
        services.AddScoped<IMeshMessageRepository, MeshMessageRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddSingleton<ITeamJoinCodeGenerator, TeamJoinCodeGenerator>();

        services.AddCronJob<SampleStatisticsJob>(configuration, SampleStatisticsJob.Name);

        return services;
    }

    /// <summary>
    /// Registers the mesh platform: one transport per configured way in (MQTT broker, TCP node, simulator), the ingest
    /// that processes what they receive, the outbox that sends, and the retention job. Add the health check with
    /// <see cref="MeshGatewayHealthCheck"/>.
    /// </summary>
    public static IServiceCollection AddMesh(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MeshOutboundOptions>(configuration.GetSection(MeshOutboundOptions.SectionName));
        services.Configure<MeshIngestOptions>(configuration.GetSection(MeshIngestOptions.SectionName));
        services.Configure<MeshRetentionOptions>(configuration.GetSection(MeshRetentionOptions.SectionName));
        services.Configure<MeshMqttOptions>(configuration.GetSection(MeshMqttOptions.SectionName));

        if (configuration.GetSection(MeshMqttOptions.SectionName).Get<MeshMqttOptions>() is { Enabled: true })
        {
            services.AddSingleton<MqttGatewayTransport>();
            services.AddSingleton<IGatewayTransport>(provider => provider.GetRequiredService<MqttGatewayTransport>());
        }

        var tcp = configuration.GetSection(MeshRadioOptions.SectionName).Get<MeshRadioOptions>() ?? new MeshRadioOptions();
        if (tcp.IsEnabled)
        {
            services.AddSingleton<IGatewayTransport>(provider => new TcpGatewayTransport(
                new TcpMeshRadio(tcp, provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ILogger<TcpMeshRadio>>()),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<TcpGatewayTransport>>()));
        }

        var simulator = configuration.GetSection(SimulatedMeshOptions.SectionName).Get<SimulatedMeshOptions>() ?? new SimulatedMeshOptions();
        if (simulator.Enabled)
        {
            services.AddSingleton(provider => new SimulatedMesh(
                simulator, provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ILogger<SimulatedMesh>>()));
            services.AddSingleton<SimulatedGatewayTransport>();
            services.AddSingleton<IGatewayTransport>(provider => provider.GetRequiredService<SimulatedGatewayTransport>());
        }

        services.AddSingleton<GatewayInbox>();
        services.AddSingleton<MeshOutbox>();
        services.AddSingleton<IMeshOutbox>(provider => provider.GetRequiredService<MeshOutbox>());
        services.AddSingleton<IGatewaySetup, GatewaySetup>();
        services.AddSingleton<IGatewayCredentialGenerator, GatewayCredentialGenerator>();
        services.AddSingleton<IContactUrlParser, ContactUrlParser>();
        services.AddSingleton<IVerificationCodeGenerator, RandomVerificationCodeGenerator>();
        services.AddHostedService<MeshIngestService>();
        services.AddHostedService<MeshMaintenanceService>();

        services.AddCronJob<NodePositionRetentionJob>(configuration, NodePositionRetentionJob.Name);
        return services;
    }

    /// <summary>Registers a cron job and binds its settings from Jobs:&lt;jobName&gt;.</summary>
    public static IServiceCollection AddCronJob<TJob>(this IServiceCollection services, IConfiguration configuration, string jobName)
        where TJob : CronJob
    {
        services.Configure<CronJobOptions>(jobName, configuration.GetSection($"Jobs:{jobName}"));
        services.AddHostedService<TJob>();
        return services;
    }
}
