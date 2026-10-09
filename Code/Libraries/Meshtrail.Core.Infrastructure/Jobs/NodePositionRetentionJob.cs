using Mediator;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.PurgeInboundBroadcasts;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.PurgeNodePositions;
using Meshtrail.Core.Infrastructure.Mesh;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meshtrail.Core.Infrastructure.Jobs;

/// <summary>
/// Deletes position history older than Meshtastic:Retention:PositionDays and received channel messages older than
/// InboundBroadcastDays, so the tables do not grow forever.
/// </summary>
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
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var sender = services.GetRequiredService<ISender>();

        var cutoff = now.AddDays(-Math.Max(1, retention.Value.PositionDays));
        LogDeleted(await sender.Send(new PurgeNodePositionsCommand(cutoff), cancellationToken), "node positions", cutoff);

        cutoff = now.AddDays(-Math.Max(1, retention.Value.InboundBroadcastDays));
        LogDeleted(await sender.Send(new PurgeInboundBroadcastsCommand(cutoff), cancellationToken), "channel messages", cutoff);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} {What} older than {Cutoff}")]
    private partial void LogDeleted(int count, string what, DateTimeOffset cutoff);
}
