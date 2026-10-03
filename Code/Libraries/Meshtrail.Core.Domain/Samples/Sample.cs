using Meshtrail.Core.Domain.Common;

namespace Meshtrail.Core.Domain.Samples;

/// <summary>
/// Reference aggregate: shows where business rules live. Copy this shape for real entities.
/// </summary>
public sealed class Sample
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int UserMaxLength = 256;

    // Private so the only ways to get a Sample are Create (new) and Rehydrate (loaded from storage).
    private Sample()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>Version stamp set by the database; used to detect two people editing the same row.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public static Sample Create(string name, string? description, string createdBy, DateTimeOffset now)
    {
        var sample = new Sample
        {
            Id = Guid.CreateVersion7(now),
            CreatedAt = now,
            CreatedBy = RequireUser(createdBy),
        };
        sample.SetDetails(name, description);
        return sample;
    }

    /// <summary>Rebuilds a Sample from stored values. Only the persistence layer should call this.</summary>
    public static Sample Rehydrate(
        Guid id,
        string name,
        string? description,
        DateTimeOffset createdAt,
        string createdBy,
        DateTimeOffset? modifiedAt,
        string? modifiedBy,
        byte[] rowVersion) => new()
    {
        Id = id,
        Name = name,
        Description = description,
        CreatedAt = createdAt,
        CreatedBy = createdBy,
        ModifiedAt = modifiedAt,
        ModifiedBy = modifiedBy,
        RowVersion = rowVersion,
    };

    public void Update(string name, string? description, string modifiedBy, DateTimeOffset now)
    {
        SetDetails(name, description);
        ModifiedAt = now;
        ModifiedBy = RequireUser(modifiedBy);
    }

    /// <summary>Stores the new version stamp the database produced after a save.</summary>
    public void SyncRowVersion(byte[] rowVersion) => RowVersion = rowVersion;

    private void SetDetails(string name, string? description)
    {
        // Trim first so "  " counts as empty and stored names never carry stray spaces.
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
        {
            throw new DomainException("A sample needs a name.");
        }

        if (trimmedName.Length > NameMaxLength)
        {
            throw new DomainException($"A sample name can be at most {NameMaxLength} characters.");
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription?.Length > DescriptionMaxLength)
        {
            throw new DomainException($"A sample description can be at most {DescriptionMaxLength} characters.");
        }

        Name = trimmedName;
        Description = trimmedDescription;
    }

    private static string RequireUser(string user) =>
        string.IsNullOrWhiteSpace(user)
            ? throw new DomainException("Every change must be linked to a user.")
            : user.Length > UserMaxLength ? user[..UserMaxLength] : user;
}
