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

    /// <summary>Outbound messages still waiting to be sent, oldest first.</summary>
    Task<IReadOnlyList<MeshMessage>> GetQueuedAsync(CancellationToken cancellationToken);

    /// <summary>Messages handed to the gateway before <paramref name="cutoff"/> that never got a delivery report.</summary>
    Task<IReadOnlyList<MeshMessage>> GetSentBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>One page of a channel or a conversation, newest first. Verification messages are left out.</summary>
    Task<PagedResult<MeshMessage>> GetPageAsync(MessageListRequest request, CancellationToken cancellationToken);

    Task AddAsync(MeshMessage message, CancellationToken cancellationToken);

    Task UpdateAsync(MeshMessage message, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
