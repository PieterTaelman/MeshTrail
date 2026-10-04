using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Persistence;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Meshtrail.Core.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Meshtrail.Core.Infrastructure.Repositories;

internal sealed class MeshMessageRepository(MeshtrailDbContext dbContext) : IMeshMessageRepository
{
    private const string Outbound = nameof(MessageDirection.Outbound);
    private const string Inbound = nameof(MessageDirection.Inbound);

    private readonly Dictionary<MeshMessage, DbMeshMessage> _rows = new(ReferenceEqualityComparer.Instance);

    private DbSet<DbMeshMessage> Messages => dbContext.Set<DbMeshMessage>();

    public async Task<MeshMessage?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Track(await Messages.FirstOrDefaultAsync(row => row.Id == id, cancellationToken));

    public async Task<MeshMessage?> GetOutboundByPacketIdAsync(uint packetId, CancellationToken cancellationToken) =>
        Track(await Messages
            .Where(row => row.PacketId == packetId && row.Direction == Outbound)
            .OrderByDescending(row => row.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken));

    public async Task<bool> InboundExistsAsync(uint fromNodeNum, uint packetId, CancellationToken cancellationToken) =>
        await Messages.AnyAsync(row => row.Direction == Inbound && row.FromNodeNum == fromNodeNum && row.PacketId == packetId, cancellationToken);

    public async Task<IReadOnlyList<MeshMessage>> GetQueuedAsync(CancellationToken cancellationToken) =>
        TrackAll(await Messages
            .Where(row => row.Direction == Outbound && row.Status == nameof(MessageStatus.Queued))
            .OrderBy(row => row.CreatedAt)
            .ToListAsync(cancellationToken));

    public async Task<IReadOnlyList<MeshMessage>> GetSentBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        TrackAll(await Messages
            .Where(row => row.Direction == Outbound && row.Status == nameof(MessageStatus.Sent) && row.SentAt < cutoff)
            .ToListAsync(cancellationToken));

    public async Task<PagedResult<MeshMessage>> GetPageAsync(MessageListRequest request, CancellationToken cancellationToken)
    {
        var query = Messages.AsNoTracking().Where(row => row.Kind != nameof(MessageKind.Verification));

        if (request.Node is { } nodeNum)
        {
            // A conversation: what this node sent us directly, and what we sent to it.
            query = query.Where(row =>
                (row.Direction == Inbound && row.FromNodeNum == nodeNum && row.ToNodeNum != null) ||
                (row.Direction == Outbound && row.ToNodeNum == nodeNum));
        }
        else
        {
            var channel = request.Channel ?? 0;
            query = query.Where(row => row.ToNodeNum == null && row.ChannelIndex == channel);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(row => row.CreatedAt)
            .ThenByDescending(row => row.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MeshMessage>([.. rows.Select(row => row.ToDomain())], totalCount, request.Page, request.PageSize);
    }

    public Task AddAsync(MeshMessage message, CancellationToken cancellationToken)
    {
        var row = message.ToDb();
        Messages.Add(row);
        _rows[message] = row;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MeshMessage message, CancellationToken cancellationToken)
    {
        if (!_rows.TryGetValue(message, out var row))
        {
            row = message.ToDb();
            Messages.Attach(row);
            _rows[message] = row;
        }

        message.CopyTo(row);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await dbContext.SaveChangesAsync(cancellationToken);

    private MeshMessage? Track(DbMeshMessage? row)
    {
        if (row is null)
        {
            return null;
        }

        var message = row.ToDomain();
        _rows[message] = row;
        return message;
    }

    private List<MeshMessage> TrackAll(List<DbMeshMessage> rows) => [.. rows.Select(row => Track(row)!)];
}
