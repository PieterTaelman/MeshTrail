using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Persistence;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Meshtrail.Core.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Meshtrail.Core.Infrastructure.Repositories;

internal sealed class MeshGatewayRepository(MeshtrailDbContext dbContext) : IMeshGatewayRepository
{
    private const string Revoked = nameof(GatewayStatus.Revoked);
    private const string Online = nameof(GatewayStatus.Online);
    private const string Pending = nameof(GatewayStatus.Pending);
    private const string Mqtt = nameof(GatewayTransport.Mqtt);

    // Pairs each gateway handed out in this scope with its EF row, so updates change the right row.
    private readonly Dictionary<MeshGateway, DbMeshGateway> _rows = new(ReferenceEqualityComparer.Instance);

    private DbSet<DbMeshGateway> Gateways => dbContext.Set<DbMeshGateway>();

    private DbSet<DbGatewayChannel> Channels => dbContext.Set<DbGatewayChannel>();

    public async Task<MeshGateway?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Track(await Gateways.FirstOrDefaultAsync(row => row.Id == id, cancellationToken));

    public async Task<MeshGateway?> GetActiveByNodeNumAsync(uint nodeNum, CancellationToken cancellationToken) =>
        Track(await Gateways.FirstOrDefaultAsync(row => row.NodeNum == nodeNum && row.Status != Revoked, cancellationToken));

    public async Task<MeshGateway?> GetByMqttUserNameAsync(string userName, CancellationToken cancellationToken) =>
        Track(await Gateways.FirstOrDefaultAsync(row => row.MqttUserName == userName, cancellationToken));

    public async Task<IReadOnlyList<MeshGateway>> GetActiveByNodeNumsAsync(IReadOnlyCollection<uint> nodeNums, CancellationToken cancellationToken)
    {
        if (nodeNums.Count == 0)
        {
            return [];
        }

        var wanted = nodeNums.Select(nodeNum => (long)nodeNum).ToList();
        var rows = await Gateways.AsNoTracking()
            .Where(row => row.NodeNum != null && wanted.Contains(row.NodeNum.Value) && row.Status != Revoked && row.Status != Pending)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.ToDomain())];
    }

    public async Task<IReadOnlyList<MeshGateway>> GetActiveForOwnerAsync(string ownerUserId, CancellationToken cancellationToken)
    {
        var rows = await Gateways.AsNoTracking()
            .Where(row => row.OwnerUserId == ownerUserId && row.Status != Revoked)
            .OrderByDescending(row => row.CreatedAt)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.ToDomain())];
    }

    public async Task<IReadOnlyList<MeshGateway>> GetActiveMqttOnBrokerAsync(string broker, CancellationToken cancellationToken)
    {
        var rows = await Gateways
            .Where(row => row.Transport == Mqtt && row.Broker == broker && row.Status != Revoked)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => Track(row)!)];
    }

    public async Task<IReadOnlyList<MeshGateway>> GetCarryingChannelAsync(string channelName, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var carrying = Channels.Where(channel => channel.ChannelName == channelName && channel.LastSeenAt >= since).Select(channel => channel.GatewayId);
        var rows = await Gateways.AsNoTracking()
            .Where(row => carrying.Contains(row.Id) && row.NodeNum != null && row.Status != Revoked)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.ToDomain())];
    }

    public async Task<IReadOnlyList<MeshGateway>> GetBoundAsync(CancellationToken cancellationToken)
    {
        var rows = await Gateways.AsNoTracking()
            .Where(row => row.NodeNum != null && row.Status != Revoked && row.Status != Pending)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => row.ToDomain())];
    }

    public async Task<(int Online, int Total)> CountAsync(CancellationToken cancellationToken)
    {
        var bound = Gateways.AsNoTracking().Where(row => row.NodeNum != null && row.Status != Revoked && row.Status != Pending);
        var online = await bound.CountAsync(row => row.Status == Online, cancellationToken);
        var total = await bound.CountAsync(cancellationToken);
        return (online, total);
    }

    public async Task<int> CountActiveForOwnerAsync(string ownerUserId, CancellationToken cancellationToken) =>
        await Gateways.CountAsync(row => row.OwnerUserId == ownerUserId && row.Status != Revoked, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetChannelsAsync(IReadOnlyCollection<Guid> gatewayIds, CancellationToken cancellationToken)
    {
        if (gatewayIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var wanted = gatewayIds.ToList();
        var rows = await Channels.AsNoTracking().Where(row => wanted.Contains(row.GatewayId)).ToListAsync(cancellationToken);
        return rows
            .GroupBy(row => row.GatewayId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)[.. group.Select(row => row.ChannelName).Order(StringComparer.Ordinal)]);
    }

    public async Task RecordChannelAsync(Guid gatewayId, string channelName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var name = channelName.Length > MeshGateway.ChannelMaxLength ? channelName[..MeshGateway.ChannelMaxLength] : channelName;
        var row = Channels.Local.FirstOrDefault(channel => channel.GatewayId == gatewayId && channel.ChannelName == name)
            ?? await Channels.FirstOrDefaultAsync(channel => channel.GatewayId == gatewayId && channel.ChannelName == name, cancellationToken);
        if (row is null)
        {
            Channels.Add(new DbGatewayChannel { GatewayId = gatewayId, ChannelName = name, FirstSeenAt = now, LastSeenAt = now });
        }
        else
        {
            row.LastSeenAt = now;
        }
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
            // Gateway did not come from this repository instance: reuse the tracked row, or attach one.
            row = Gateways.Local.FirstOrDefault(candidate => candidate.Id == gateway.Id);
            if (row is null)
            {
                row = gateway.ToDb();
                Gateways.Attach(row);
            }

            _rows[gateway] = row;
        }

        gateway.CopyTo(row);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await dbContext.SaveChangesAsync(cancellationToken);

    private MeshGateway? Track(DbMeshGateway? row)
    {
        if (row is null)
        {
            return null;
        }

        var gateway = row.ToDomain();
        _rows[gateway] = row;
        return gateway;
    }
}
