using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Samples;
using Meshtrail.Core.Domain.Samples;
using Meshtrail.Core.Infrastructure.Persistence;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Meshtrail.Core.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Meshtrail.Core.Infrastructure.Repositories;

internal sealed class SampleRepository(MeshtrailDbContext dbContext) : ISampleRepository
{
    // Pairs each domain object handed out in this scope with its EF row,
    // so we can update the right row and copy the new RowVersion back after saving.
    private readonly Dictionary<Sample, DbSample> _rows = new(ReferenceEqualityComparer.Instance);

    private DbSet<DbSample> Samples => dbContext.Set<DbSample>();

    public async Task<Sample?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await Samples.FirstOrDefaultAsync(sample => sample.Id == id, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var sample = row.ToDomain();
        _rows[sample] = row;
        return sample;
    }

    public Task AddAsync(Sample sample, CancellationToken cancellationToken)
    {
        var row = sample.ToDb();
        Samples.Add(row);
        _rows[sample] = row;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Sample sample, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        var row = GetOrAttachRow(sample);
        sample.CopyTo(row);

        // Compare against the version the CLIENT saw, not the one we just loaded, to catch edits made in between.
        dbContext.Entry(row).Property(r => r.RowVersion).OriginalValue = expectedRowVersion;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Sample sample, CancellationToken cancellationToken)
    {
        Samples.Remove(GetOrAttachRow(sample));
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
            throw new ConcurrencyException("The sample was changed or deleted by someone else. Reload it and try again.", exception);
        }

        foreach (var (sample, row) in _rows)
        {
            if (dbContext.Entry(row).State != EntityState.Detached)
            {
                sample.SyncRowVersion(row.RowVersion);
            }
        }
    }

    public async Task<PagedResult<SampleGridItemDto>> GetGridAsync(SampleGridRequest request, CancellationToken cancellationToken)
    {
        var query = Samples.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(sample => sample.Name.Contains(search) || (sample.Description != null && sample.Description.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(request.CreatedBy))
        {
            query = query.Where(sample => sample.CreatedBy == request.CreatedBy);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await Sort(query, request.SortBy, request.SortDescending)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(sample => new SampleGridItemDto(sample.Id, sample.Name, sample.Description, sample.CreatedAt, sample.CreatedBy))
            .ToListAsync(cancellationToken);

        return new PagedResult<SampleGridItemDto>(items, totalCount, request.Page, request.PageSize);
    }

    public async Task<SampleFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        var createdBy = await Samples.AsNoTracking()
            .Select(sample => sample.CreatedBy)
            .Distinct()
            .OrderBy(user => user)
            .ToListAsync(cancellationToken);

        return new SampleFilterOptionsDto(createdBy);
    }

    private static IQueryable<DbSample> Sort(IQueryable<DbSample> query, string? sortBy, bool descending)
    {
        // Id as a tie-breaker keeps paging stable when sort values are equal.
        IOrderedQueryable<DbSample> ordered = sortBy switch
        {
            SampleGridSortColumns.Name => descending ? query.OrderByDescending(s => s.Name) : query.OrderBy(s => s.Name),
            SampleGridSortColumns.CreatedBy => descending ? query.OrderByDescending(s => s.CreatedBy) : query.OrderBy(s => s.CreatedBy),
            SampleGridSortColumns.CreatedAt => descending ? query.OrderByDescending(s => s.CreatedAt) : query.OrderBy(s => s.CreatedAt),
            _ => query.OrderByDescending(s => s.CreatedAt),
        };

        return ordered.ThenBy(s => s.Id);
    }

    private DbSample GetOrAttachRow(Sample sample)
    {
        if (_rows.TryGetValue(sample, out var row))
        {
            return row;
        }

        // Sample did not come from this repository instance: attach a row built from it.
        row = sample.ToDb();
        Samples.Attach(row);
        _rows[sample] = row;
        return row;
    }
}
