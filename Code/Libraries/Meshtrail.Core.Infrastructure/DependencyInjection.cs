using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Infrastructure.Jobs;
using Meshtrail.Core.Infrastructure.Repositories;
using Meshtrail.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddCronJob<SampleStatisticsJob>(configuration, SampleStatisticsJob.Name);

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
