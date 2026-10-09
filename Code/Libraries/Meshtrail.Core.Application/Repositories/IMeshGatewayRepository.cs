using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.Repositories;

/// <summary>Storage for <see cref="MeshGateway"/> and the channel names each gateway carries.</summary>
public interface IMeshGatewayRepository
{
    Task<MeshGateway?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The active (not revoked) gateway on this node, if any (there is at most one).</summary>
    Task<MeshGateway?> GetActiveByNodeNumAsync(uint nodeNum, CancellationToken cancellationToken);

    /// <summary>The gateway with this MQTT login, also when revoked (logins are never reused).</summary>
    Task<MeshGateway?> GetByMqttUserNameAsync(string userName, CancellationToken cancellationToken);

    /// <summary>Active gateways on these nodes (read-only).</summary>
    Task<IReadOnlyList<MeshGateway>> GetActiveByNodeNumsAsync(IReadOnlyCollection<uint> nodeNums, CancellationToken cancellationToken);

    /// <summary>The user's gateways that are not revoked, newest first (read-only).</summary>
    Task<IReadOnlyList<MeshGateway>> GetActiveForOwnerAsync(string ownerUserId, CancellationToken cancellationToken);

    /// <summary>Active MQTT gateways of one broker (tracked, so the caller can change them).</summary>
    Task<IReadOnlyList<MeshGateway>> GetActiveMqttOnBrokerAsync(string broker, CancellationToken cancellationToken);

    /// <summary>Active, bound gateways that uplinked on this channel since <paramref name="since"/> (read-only).</summary>
    Task<IReadOnlyList<MeshGateway>> GetCarryingChannelAsync(string channelName, DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>Active gateways that are tied to a node (read-only), for the gateway map layer.</summary>
    Task<IReadOnlyList<MeshGateway>> GetBoundAsync(CancellationToken cancellationToken);

    /// <summary>How many gateways are online, and how many are tied to a node in total (revoked excluded).</summary>
    Task<(int Online, int Total)> CountAsync(CancellationToken cancellationToken);

    Task<int> CountActiveForOwnerAsync(string ownerUserId, CancellationToken cancellationToken);

    /// <summary>Channel names per gateway, sorted.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetChannelsAsync(IReadOnlyCollection<Guid> gatewayIds, CancellationToken cancellationToken);

    /// <summary>Remembers that the gateway uplinked on this channel (staged; written by SaveChangesAsync).</summary>
    Task RecordChannelAsync(Guid gatewayId, string channelName, DateTimeOffset now, CancellationToken cancellationToken);

    Task AddAsync(MeshGateway gateway, CancellationToken cancellationToken);

    Task UpdateAsync(MeshGateway gateway, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
