using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Persistence;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Meshtrail.Core.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Meshtrail.Core.Infrastructure.Repositories;

internal sealed class NodeTracerouteRepository(MeshtrailDbContext dbContext) : INodeTracerouteRepository
{
    private readonly Dictionary<NodeTraceroute, DbNodeTraceroute> _rows = new(ReferenceEqualityComparer.Instance);

    private DbSet<DbNodeTraceroute> Traceroutes => dbContext.Set<DbNodeTraceroute>();

    public Task AddAsync(NodeTraceroute traceroute, CancellationToken cancellationToken)
    {
        var row = traceroute.ToDb();
        Traceroutes.Add(row);
        _rows[traceroute] = row;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(NodeTraceroute traceroute, CancellationToken cancellationToken)
    {
        if (!_rows.TryGetValue(traceroute, out var row))
        {
            row = traceroute.ToDb();
            Traceroutes.Attach(row);
            _rows[traceroute] = row;
        }

        traceroute.CopyTo(row);
        return Task.CompletedTask;
    }

    public async Task<NodeTraceroute?> GetByPacketIdAsync(uint nodeNum, uint packetId, CancellationToken cancellationToken) =>
        Track(await Traceroutes.FirstOrDefaultAsync(row => row.NodeNum == nodeNum && row.PacketId == packetId, cancellationToken));

    public async Task<NodeTraceroute?> GetLatestForNodeAsync(uint nodeNum, CancellationToken cancellationToken) =>
        Track(await Traceroutes
            .Where(row => row.NodeNum == nodeNum)
            .OrderByDescending(row => row.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken));

    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await dbContext.SaveChangesAsync(cancellationToken);

    private NodeTraceroute? Track(DbNodeTraceroute? row)
    {
        if (row is null)
        {
            return null;
        }

        var traceroute = row.ToDomain();
        _rows[traceroute] = row;
        return traceroute;
    }
}
