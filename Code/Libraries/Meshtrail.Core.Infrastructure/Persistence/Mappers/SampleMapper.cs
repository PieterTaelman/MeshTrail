using Meshtrail.Core.Domain.Samples;
using Meshtrail.Core.Infrastructure.Persistence.Entities;

namespace Meshtrail.Core.Infrastructure.Persistence.Mappers;

/// <summary>Domain ↔ Db conversion. The only place that knows both shapes.</summary>
internal static class SampleMapper
{
    public static Sample ToDomain(this DbSample row) => Sample.Rehydrate(
        row.Id,
        row.Name,
        row.Description,
        row.CreatedAt,
        row.CreatedBy,
        row.ModifiedAt,
        row.ModifiedBy,
        row.RowVersion);

    public static DbSample ToDb(this Sample sample)
    {
        var row = new DbSample { Id = sample.Id, RowVersion = sample.RowVersion };
        sample.CopyTo(row);
        return row;
    }

    /// <summary>Copies the editable values onto an existing row. Id and RowVersion are left alone on purpose.</summary>
    public static void CopyTo(this Sample sample, DbSample row)
    {
        row.Name = sample.Name;
        row.Description = sample.Description;
        row.CreatedAt = sample.CreatedAt;
        row.CreatedBy = sample.CreatedBy;
        row.ModifiedAt = sample.ModifiedAt;
        row.ModifiedBy = sample.ModifiedBy;
    }
}
