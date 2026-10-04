using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Persistence;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Meshtrail.Core.Infrastructure.Persistence.Mappers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Meshtrail.Core.Infrastructure.Repositories;

internal sealed class NodeRegistrationRepository(MeshtrailDbContext dbContext) : INodeRegistrationRepository
{
    private static readonly string[] ActiveStatuses = [nameof(RegistrationStatus.Claimed), nameof(RegistrationStatus.Verified)];

    private readonly Dictionary<NodeRegistration, DbNodeRegistration> _rows = new(ReferenceEqualityComparer.Instance);

    private DbSet<DbNodeRegistration> Registrations => dbContext.Set<DbNodeRegistration>();

    public async Task<NodeRegistration?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Track(await Registrations.FirstOrDefaultAsync(row => row.Id == id, cancellationToken));

    public async Task<NodeRegistration?> GetActiveForNodeAsync(uint nodeNum, CancellationToken cancellationToken) =>
        Track(await Registrations.FirstOrDefaultAsync(row => row.NodeNum == nodeNum && ActiveStatuses.Contains(row.Status), cancellationToken));

    public async Task<NodeRegistration?> GetByVerificationMessageAsync(Guid messageId, CancellationToken cancellationToken) =>
        Track(await Registrations.FirstOrDefaultAsync(row => row.VerificationMessageId == messageId, cancellationToken));

    public async Task<IReadOnlyList<NodeRegistration>> GetActiveForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var rows = await Registrations
            .Where(row => row.UserId == userId && ActiveStatuses.Contains(row.Status))
            .OrderByDescending(row => row.ClaimedAt)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => Track(row)!)];
    }

    public async Task<IReadOnlySet<uint>> GetVerifiedNodeNumsAsync(IReadOnlyCollection<uint> nodeNums, CancellationToken cancellationToken)
    {
        if (nodeNums.Count == 0)
        {
            return new HashSet<uint>();
        }

        var wanted = nodeNums.Select(nodeNum => (long)nodeNum).ToList();
        var verified = await Registrations.AsNoTracking()
            .Where(row => row.Status == nameof(RegistrationStatus.Verified) && wanted.Contains(row.NodeNum))
            .Select(row => row.NodeNum)
            .ToListAsync(cancellationToken);
        return verified.Select(nodeNum => (uint)nodeNum).ToHashSet();
    }

    public Task AddAsync(NodeRegistration registration, CancellationToken cancellationToken)
    {
        var row = registration.ToDb();
        Registrations.Add(row);
        _rows[registration] = row;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(NodeRegistration registration, CancellationToken cancellationToken)
    {
        if (!_rows.TryGetValue(registration, out var row))
        {
            row = registration.ToDb();
            Registrations.Attach(row);
            _rows[registration] = row;
        }

        registration.CopyTo(row);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyException("The registration was changed at the same time. Reload and try again.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The unique index allows one active registration per node: someone else was faster.
            throw new ConcurrencyException("Someone else registered this node at the same moment. Try again.", exception);
        }

        foreach (var (registration, row) in _rows)
        {
            if (dbContext.Entry(row).State != EntityState.Detached)
            {
                registration.SyncRowVersion(row.RowVersion);
            }
        }
    }

    private NodeRegistration? Track(DbNodeRegistration? row)
    {
        if (row is null)
        {
            return null;
        }

        var registration = row.ToDomain();
        _rows[registration] = row;
        return registration;
    }
}
