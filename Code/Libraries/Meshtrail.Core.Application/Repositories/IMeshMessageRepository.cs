using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.Repositories;

/// <summary>Storage for <see cref="MeshMessage"/>.</summary>
public interface IMeshMessageRepository
{
    Task<MeshMessage?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Our own message with this packet id (delivery reports refer to it).</summary>
    Task<MeshMessage?> GetOutboundByPacketIdAsync(uint packetId, CancellationToken cancellationToken);

    Task<bool> InboundExistsAsync(uint fromNodeNum, uint packetId, CancellationToken cancellationToken);

    /// <summary>Outbound messages still waiting to be sent through this gateway, oldest first.</summary>
    Task<IReadOnlyList<MeshMessage>> GetQueuedAsync(uint gatewayNodeNum, CancellationToken cancellationToken);

    /// <summary>Outbound messages queued before <paramref name="cutoff"/> that never went out (their gateway stayed offline).</summary>
    Task<IReadOnlyList<MeshMessage>> GetQueuedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>Deletes received channel messages older than <paramref name="cutoff"/> right away. Returns the number of rows.</summary>
    Task<int> DeleteInboundBroadcastsBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>Messages handed to the gateway before <paramref name="cutoff"/> that never got a delivery report.</summary>
    Task<IReadOnlyList<MeshMessage>> GetSentBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>User ids of the people who wrote to this node (verification messages do not count).</summary>
    Task<IReadOnlyList<string>> GetAuthorIdsToNodeAsync(uint nodeNum, CancellationToken cancellationToken);

    /// <summary>One page of a direct-message conversation or a team chat, newest first. Verification messages are left out.</summary>
    Task<PagedResult<MeshMessage>> GetPageAsync(MessageListRequest request, CancellationToken cancellationToken);

    Task AddAsync(MeshMessage message, CancellationToken cancellationToken);

    Task UpdateAsync(MeshMessage message, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
