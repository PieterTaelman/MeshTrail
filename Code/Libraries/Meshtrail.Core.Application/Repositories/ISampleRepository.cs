using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Samples;
using Meshtrail.Core.Domain.Samples;

namespace Meshtrail.Core.Application.Repositories;

/// <summary>
/// Storage for <see cref="Sample"/>. Defined here, implemented in Infrastructure, so handlers never see EF.
/// Add/Update/Delete only stage changes; nothing is written until SaveChangesAsync.
/// </summary>
public interface ISampleRepository
{
    Task<Sample?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Sample sample, CancellationToken cancellationToken);

    /// <summary>Stages an update. Save fails with ConcurrencyException if the row no longer has <paramref name="expectedRowVersion"/>.</summary>
    Task UpdateAsync(Sample sample, byte[] expectedRowVersion, CancellationToken cancellationToken);

    Task DeleteAsync(Sample sample, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Read side for the list screen: filters, sorts and pages in SQL and returns grid rows directly.</summary>
    Task<PagedResult<SampleGridItemDto>> GetGridAsync(SampleGridRequest request, CancellationToken cancellationToken);

    Task<SampleFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken);
}
