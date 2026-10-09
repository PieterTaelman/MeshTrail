using Mediator;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.FailTimedOutMessages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>Every 15 seconds: messages without a delivery report in time, or stuck behind an offline gateway, become Failed.</summary>
public sealed partial class MeshMaintenanceService(
    IServiceScopeFactory scopeFactory,
    IOptions<MeshOutboundOptions> options,
    TimeProvider timeProvider,
    ILogger<MeshMaintenanceService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, timeProvider, stoppingToken);
                var now = timeProvider.GetUtcNow();
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new FailTimedOutMessagesCommand(now - options.Value.AckTimeout, now - options.Value.QueueTimeout), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // The database may be down; try again next round.
                LogSweepFailed(exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Checking for timed-out messages failed")]
    private partial void LogSweepFailed(Exception exception);
}
