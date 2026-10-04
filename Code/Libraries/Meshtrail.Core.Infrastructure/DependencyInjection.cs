using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Infrastructure.Jobs;
using Meshtrail.Core.Infrastructure.Mesh;
using Meshtrail.Core.Infrastructure.Repositories;
using Meshtrail.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Meshtrail.Mesh.Radio;

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
        services.AddScoped<INodeTracerouteRepository, NodeTracerouteRepository>();
        services.AddScoped<INodeRegistrationRepository, NodeRegistrationRepository>();
        services.AddScoped<IMeshMessageRepository, MeshMessageRepository>();

        services.AddCronJob<SampleStatisticsJob>(configuration, SampleStatisticsJob.Name);

        return services;
    }

    /// <summary>
    /// Registers the Meshtastic gateway: the radio chosen by Meshtastic:Gateway:Mode, the worker that runs it inside
    /// this process, and the position retention job. Add the health check with <see cref="MeshGatewayHealthCheck"/>.
    /// </summary>
    public static IServiceCollection AddMesh(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MeshRadioOptions>(configuration.GetSection(MeshRadioOptions.SectionName));
        services.Configure<MeshOutboundOptions>(configuration.GetSection(MeshOutboundOptions.SectionName));
        services.Configure<MeshRetentionOptions>(configuration.GetSection(MeshRetentionOptions.SectionName));

        services.AddSingleton<IMeshRadio>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<MeshRadioOptions>>().Value;
            var time = provider.GetRequiredService<TimeProvider>();
            return options.Mode == MeshRadioMode.Simulated
                ? new SimulatedMeshRadio(options, time, provider.GetRequiredService<ILogger<SimulatedMeshRadio>>())
                : new TcpMeshRadio(options, time, provider.GetRequiredService<ILogger<TcpMeshRadio>>());
        });

        services.AddSingleton<MeshGatewayService>();
        services.AddSingleton<IContactUrlParser, ContactUrlParser>();
        services.AddSingleton<IVerificationCodeGenerator, RandomVerificationCodeGenerator>();
        services.AddSingleton<IMeshGateway>(provider => provider.GetRequiredService<MeshGatewayService>());
        services.AddHostedService<MeshGatewayWorker>();

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
