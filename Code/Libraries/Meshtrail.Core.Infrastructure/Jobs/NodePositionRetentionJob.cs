using Mediator;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.PurgeNodePositions;
using Meshtrail.Core.Infrastructure.Mesh;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meshtrail.Core.Infrastructure.Jobs;

/// <summary>Deletes position history older than Meshtastic:Retention:PositionDays, so the table does not grow forever.</summary>
internal sealed partial class NodePositionRetentionJob(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<CronJobOptions> options,
    IOptions<MeshRetentionOptions> retention,
    TimeProvider timeProvider,
    ILogger<NodePositionRetentionJob> logger)
    : CronJob(scopeFactory, options, timeProvider, logger)
{
    public const string Name = "NodePositionRetention";

    protected override string JobName => Name;

    protected override async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var cutoff = services.GetRequiredService<TimeProvider>().GetUtcNow().AddDays(-Math.Max(1, retention.Value.PositionDays));
        var deleted = await services.GetRequiredService<ISender>().Send(new PurgeNodePositionsCommand(cutoff), cancellationToken);
        LogDeleted(deleted, cutoff);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} node positions older than {Cutoff}")]
    private partial void LogDeleted(int count, DateTimeOffset cutoff);
}
