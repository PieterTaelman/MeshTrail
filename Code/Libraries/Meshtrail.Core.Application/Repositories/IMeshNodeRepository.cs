using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.Repositories;

/// <summary>What the node list can filter on (already validated).</summary>
public sealed record NodeFilter(
    string? Search,
    bool? Registered,
    bool? Online,
    BoundingBox? Box,
    string? OwnerUserId,
    DateTimeOffset OnlineSince);

/// <summary>Storage for <see cref="MeshNode"/> and its position history. Changes are written by SaveChangesAsync.</summary>
public interface IMeshNodeRepository
{
    Task<MeshNode?> GetAsync(uint nodeNum, CancellationToken cancellationToken);

    /// <summary>These nodes, when known (read-only), e.g. to show gateway names.</summary>
    Task<IReadOnlyList<MeshNode>> GetManyAsync(IReadOnlyCollection<uint> nodeNums, CancellationToken cancellationToken);

    Task AddAsync(MeshNode node, CancellationToken cancellationToken);

    Task UpdateAsync(MeshNode node, CancellationToken cancellationToken);

    Task AddPositionAsync(NodePosition position, CancellationToken cancellationToken);

    /// <summary>Deletes history older than <paramref name="cutoff"/> right away (not staged). Returns the number of rows.</summary>
    Task<int> DeletePositionsBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Read side of the node list: filters and pages in SQL, most recently heard first.</summary>
    Task<PagedResult<MeshNode>> GetPageAsync(NodeFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Nodes with a known position, optionally only inside <paramref name="box"/> (for the map). At most
    /// <paramref name="limit"/>, most recently heard first, so a zoomed-out world map stays fast.
    /// </summary>
    Task<IReadOnlyList<MeshNode>> GetWithPositionAsync(BoundingBox? box, int limit, CancellationToken cancellationToken);
}
