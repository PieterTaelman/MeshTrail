using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Persistence;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Meshtrail.Core.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Meshtrail.Core.Infrastructure.Repositories;

internal sealed class NodeReceptionRepository(MeshtrailDbContext dbContext) : INodeReceptionRepository
{
    private DbSet<DbNodeReception> Receptions => dbContext.Set<DbNodeReception>();

    public async Task<IReadOnlyList<NodeReception>> GetForNodeAsync(uint nodeNum, CancellationToken cancellationToken)
    {
        var rows = await Receptions.AsNoTracking().Where(row => row.NodeNum == nodeNum).ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.ToDomain())];
    }

    public async Task RecordAsync(
        uint nodeNum, uint gatewayNodeNum, DateTimeOffset heardAt, double? snr, int? rssi, int? hopsAway, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = Receptions.Local.FirstOrDefault(candidate => candidate.NodeNum == nodeNum && candidate.GatewayNodeNum == gatewayNodeNum)
            ?? await Receptions.FirstOrDefaultAsync(candidate => candidate.NodeNum == nodeNum && candidate.GatewayNodeNum == gatewayNodeNum, cancellationToken);

        if (row is null)
        {
            var reception = NodeReception.Record(nodeNum, gatewayNodeNum, heardAt, snr, rssi, hopsAway, now);
            var newRow = new DbNodeReception();
            reception.CopyTo(newRow);
            Receptions.Add(newRow);
            return;
        }

        var existing = row.ToDomain();
        if (existing.Update(heardAt, snr, rssi, hopsAway, now))
        {
            existing.CopyTo(row);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await dbContext.SaveChangesAsync(cancellationToken);
}
