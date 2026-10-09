using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Persistence;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Meshtrail.Core.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Meshtrail.Core.Infrastructure.Repositories;

internal sealed class MeshNodeRepository(MeshtrailDbContext dbContext) : IMeshNodeRepository
{
    // Pairs each node handed out in this scope with its EF row, so updates change the right row.
    private readonly Dictionary<MeshNode, DbMeshNode> _rows = new(ReferenceEqualityComparer.Instance);

    private DbSet<DbMeshNode> Nodes => dbContext.Set<DbMeshNode>();

    private DbSet<DbNodePosition> Positions => dbContext.Set<DbNodePosition>();

    public async Task<MeshNode?> GetAsync(uint nodeNum, CancellationToken cancellationToken)
    {
        var row = await Nodes.FirstOrDefaultAsync(node => node.NodeNum == nodeNum, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var node = row.ToDomain();
        _rows[node] = row;
        return node;
    }

    public async Task<IReadOnlyList<MeshNode>> GetManyAsync(IReadOnlyCollection<uint> nodeNums, CancellationToken cancellationToken)
    {
        if (nodeNums.Count == 0)
        {
            return [];
        }

        var wanted = nodeNums.Select(nodeNum => (long)nodeNum).ToList();
        var rows = await Nodes.AsNoTracking().Where(node => wanted.Contains(node.NodeNum)).ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.ToDomain())];
    }

    public Task AddAsync(MeshNode node, CancellationToken cancellationToken)
    {
        var row = node.ToDb();
        Nodes.Add(row);
        _rows[node] = row;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MeshNode node, CancellationToken cancellationToken)
    {
        if (!_rows.TryGetValue(node, out var row))
        {
            // Node did not come from this repository instance: attach a row built from it.
            row = node.ToDb();
            Nodes.Attach(row);
            _rows[node] = row;
        }

        node.CopyTo(row);
        return Task.CompletedTask;
    }

    public Task AddPositionAsync(NodePosition position, CancellationToken cancellationToken)
    {
        Positions.Add(position.ToDb());
        return Task.CompletedTask;
    }

    public async Task<int> DeletePositionsBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        await Positions.Where(position => position.ReceivedAt < cutoff).ExecuteDeleteAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await dbContext.SaveChangesAsync(cancellationToken);

    public async Task<PagedResult<MeshNode>> GetPageAsync(NodeFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = Nodes.AsNoTracking();

        if (filter.Search is { } search)
        {
            query = query.Where(node => node.LongName.Contains(search) || node.ShortName.Contains(search) || node.NodeId.Contains(search));
        }

        if (filter.Box is { } box)
        {
            query = InBox(query, box);
        }

        var onlineSince = filter.OnlineSince;
        query = filter.Online switch
        {
            true => query.Where(node => node.LastHeardAt >= onlineSince),
            false => query.Where(node => node.LastHeardAt == null || node.LastHeardAt < onlineSince),
            null => query,
        };

        var verified = dbContext.Set<DbNodeRegistration>().Where(registration => registration.Status == nameof(RegistrationStatus.Verified));
        if (filter.Registered is { } registered)
        {
            var verifiedNodes = verified.Select(registration => registration.NodeNum);
            query = registered ? query.Where(node => verifiedNodes.Contains(node.NodeNum)) : query.Where(node => !verifiedNodes.Contains(node.NodeNum));
        }

        if (filter.OwnerUserId is { } ownerUserId)
        {
            var mine = verified.Where(registration => registration.UserId == ownerUserId).Select(registration => registration.NodeNum);
            query = query.Where(node => mine.Contains(node.NodeNum));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Most recently heard first; never-heard nodes last; NodeNum keeps paging stable.
        var rows = await query
            .OrderByDescending(node => node.LastHeardAt != null)
            .ThenByDescending(node => node.LastHeardAt)
            .ThenBy(node => node.NodeNum)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MeshNode>([.. rows.Select(row => row.ToDomain())], totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<MeshNode>> GetWithPositionAsync(BoundingBox? box, int limit, CancellationToken cancellationToken)
    {
        var query = Nodes.AsNoTracking().Where(node => node.Latitude != null && node.Longitude != null && node.PositionTime != null);
        if (box is not null)
        {
            query = InBox(query, box);
        }

        var rows = await query
            .OrderByDescending(node => node.LastHeardAt)
            .ThenBy(node => node.NodeNum)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.ToDomain())];
    }

    /// <summary>Nodes whose last position is inside the box. A box crossing the 180° meridian has West &gt; East.</summary>
    private static IQueryable<DbMeshNode> InBox(IQueryable<DbMeshNode> query, BoundingBox box)
    {
        query = query.Where(node => node.Latitude >= box.South && node.Latitude <= box.North);
        return box.West <= box.East
            ? query.Where(node => node.Longitude >= box.West && node.Longitude <= box.East)
            : query.Where(node => node.Longitude >= box.West || node.Longitude <= box.East);
    }
}
