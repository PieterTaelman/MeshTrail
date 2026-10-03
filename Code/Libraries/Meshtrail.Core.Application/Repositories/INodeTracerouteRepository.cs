using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.Repositories;

/// <summary>Storage for <see cref="NodeTraceroute"/>.</summary>
public interface INodeTracerouteRepository
{
    Task AddAsync(NodeTraceroute traceroute, CancellationToken cancellationToken);

    Task UpdateAsync(NodeTraceroute traceroute, CancellationToken cancellationToken);

    /// <summary>The traceroute we sent to <paramref name="nodeNum"/> with this packet id, if any.</summary>
    Task<NodeTraceroute?> GetByPacketIdAsync(uint nodeNum, uint packetId, CancellationToken cancellationToken);

    Task<NodeTraceroute?> GetLatestForNodeAsync(uint nodeNum, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
