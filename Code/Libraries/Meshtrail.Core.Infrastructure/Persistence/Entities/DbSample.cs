namespace Meshtrail.Core.Infrastructure.Persistence.Entities;

/// <summary>
/// EF row for dbo.Samples. Plain data only: business rules stay in the domain Sample, and this type never leaves Infrastructure.
/// </summary>
internal sealed class DbSample
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTimeOffset? ModifiedAt { get; set; }

    public string? ModifiedBy { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
