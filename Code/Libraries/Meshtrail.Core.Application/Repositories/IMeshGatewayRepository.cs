using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.Repositories;

/// <summary>Storage for <see cref="MeshGateway"/> (one row per gateway key).</summary>
public interface IMeshGatewayRepository
{
    Task<MeshGateway?> GetAsync(string gatewayKey, CancellationToken cancellationToken);

    Task AddAsync(MeshGateway gateway, CancellationToken cancellationToken);

    Task UpdateAsync(MeshGateway gateway, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
