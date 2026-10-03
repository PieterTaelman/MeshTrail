using Meshtrail.Core.Contracts.Samples;
using Meshtrail.Core.Domain.Samples;

namespace Meshtrail.Core.Application.UseCases.Samples;

/// <summary>Domain → contract mapping, kept in one place so every handler returns the same shape.</summary>
internal static class SampleMappings
{
    public static SampleDto ToDto(this Sample sample) => new(
        sample.Id,
        sample.Name,
        sample.Description,
        sample.CreatedAt,
        sample.CreatedBy,
        sample.ModifiedAt,
        sample.ModifiedBy,
        Convert.ToBase64String(sample.RowVersion));
}
