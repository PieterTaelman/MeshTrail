using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.PurgeInboundBroadcasts;

/// <summary>Deletes received channel messages older than <paramref name="Before"/>. Returns how many rows were removed.</summary>
public sealed record PurgeInboundBroadcastsCommand(DateTimeOffset Before) : ICommand<int>;
