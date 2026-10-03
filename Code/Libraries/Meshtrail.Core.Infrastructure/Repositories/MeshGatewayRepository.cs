using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Persistence;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Meshtrail.Core.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Meshtrail.Core.Infrastructure.Repositories;

internal sealed class MeshGatewayRepository(MeshtrailDbContext dbContext) : IMeshGatewayRepository
{
    private readonly Dictionary<MeshGateway, DbMeshGateway> _rows = new(ReferenceEqualityComparer.Instance);

    private DbSet<DbMeshGateway> Gateways => dbContext.Set<DbMeshGateway>();

    public async Task<MeshGateway?> GetAsync(string gatewayKey, CancellationToken cancellationToken)
    {
        var row = await Gateways.FirstOrDefaultAsync(gateway => gateway.GatewayKey == gatewayKey, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var gateway = row.ToDomain();
        _rows[gateway] = row;
        return gateway;
    }

    public Task AddAsync(MeshGateway gateway, CancellationToken cancellationToken)
    {
        var row = gateway.ToDb();
        Gateways.Add(row);
        _rows[gateway] = row;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MeshGateway gateway, CancellationToken cancellationToken)
    {
        if (!_rows.TryGetValue(gateway, out var row))
        {
            row = gateway.ToDb();
            Gateways.Attach(row);
            _rows[gateway] = row;
        }

        gateway.CopyTo(row);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await dbContext.SaveChangesAsync(cancellationToken);
}
