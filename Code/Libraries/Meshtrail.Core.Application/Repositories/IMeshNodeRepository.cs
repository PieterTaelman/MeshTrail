using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.Repositories;

/// <summary>Storage for <see cref="MeshNode"/> and its position history. Changes are written by SaveChangesAsync.</summary>
public interface IMeshNodeRepository
{
    Task<MeshNode?> GetAsync(uint nodeNum, CancellationToken cancellationToken);

    Task AddAsync(MeshNode node, CancellationToken cancellationToken);

    Task UpdateAsync(MeshNode node, CancellationToken cancellationToken);

    Task AddPositionAsync(NodePosition position, CancellationToken cancellationToken);

    /// <summary>Deletes history older than <paramref name="cutoff"/> right away (not staged). Returns the number of rows.</summary>
    Task<int> DeletePositionsBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Read side of the node list: filters and pages in SQL, most recently heard first.</summary>
    Task<PagedResult<MeshNode>> GetPageAsync(NodeListRequest request, DateTimeOffset onlineSince, CancellationToken cancellationToken);

    /// <summary>Nodes with a known position, optionally only inside <paramref name="box"/> (for the map).</summary>
    Task<IReadOnlyList<MeshNode>> GetWithPositionAsync(BoundingBox? box, CancellationToken cancellationToken);
}
