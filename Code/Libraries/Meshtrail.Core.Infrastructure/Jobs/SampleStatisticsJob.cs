using Mediator;
using Meshtrail.Core.Application.UseCases.Samples.Queries.GetSampleGrid;
using Meshtrail.Core.Contracts.Samples;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meshtrail.Core.Infrastructure.Jobs;

/// <summary>
/// Example cron job: logs how many samples exist. It sends a query through Mediator,
/// so jobs reuse use cases (with validation and logging) instead of duplicating logic.
/// </summary>
internal sealed partial class SampleStatisticsJob(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<CronJobOptions> options,
    TimeProvider timeProvider,
    ILogger<SampleStatisticsJob> logger)
    : CronJob(scopeFactory, options, timeProvider, logger)
{
    public const string Name = "SampleStatistics";

    protected override string JobName => Name;

    protected override async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var sender = services.GetRequiredService<ISender>();
        var page = await sender.Send(new GetSampleGridQuery(new SampleGridRequest { PageSize = 1 }), cancellationToken);
        LogSampleCount(page.TotalCount);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "There are {SampleCount} samples")]
    private partial void LogSampleCount(int sampleCount);
}
