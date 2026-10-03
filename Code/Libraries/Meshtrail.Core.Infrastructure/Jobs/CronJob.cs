using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meshtrail.Core.Infrastructure.Jobs;

/// <summary>Settings every cron job reads from Jobs:&lt;JobName&gt; in configuration.</summary>
public sealed class CronJobOptions
{
    public bool Enabled { get; set; }

    /// <summary>Standard 5-field cron expression, e.g. "*/5 * * * *" = every 5 minutes.</summary>
    public string Cron { get; set; } = string.Empty;

    /// <summary>Time zone the cron expression is read in. Defaults to the server's local zone.</summary>
    public string? TimeZone { get; set; }
}

/// <summary>
/// Base class for scheduled work (replaces SysLib.Scheduling). Each run gets its own DI scope,
/// so jobs can use scoped services like ISender and repositories exactly like a web request does.
/// </summary>
public abstract partial class CronJob(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<CronJobOptions> options,
    TimeProvider timeProvider,
    ILogger logger) : BackgroundService
{
    /// <summary>Name of the configuration section under Jobs: and of the named options.</summary>
    protected abstract string JobName { get; }

    /// <summary>The actual work. Throwing is fine: the error is logged and the next run still happens.</summary>
    protected abstract Task RunAsync(IServiceProvider services, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Get(JobName);
        if (!settings.Enabled)
        {
            LogDisabled(JobName);
            return;
        }

        var expression = CronExpression.Parse(settings.Cron);
        var zone = settings.TimeZone is null ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = timeProvider.GetUtcNow();
            var next = expression.GetNextOccurrence(now, zone);
            if (next is null)
            {
                return;
            }

            await Task.Delay(next.Value - now, timeProvider, stoppingToken);
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await RunAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The app is shutting down; stop quietly.
        }
        catch (Exception exception)
        {
            // Swallow so one failed run does not stop the schedule forever.
            LogRunFailed(exception, JobName);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cron job {JobName} is disabled")]
    private partial void LogDisabled(string jobName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Cron job {JobName} failed")]
    private partial void LogRunFailed(Exception exception, string jobName);
}
