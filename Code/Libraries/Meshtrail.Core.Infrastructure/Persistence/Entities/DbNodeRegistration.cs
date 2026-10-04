namespace Meshtrail.Core.Infrastructure.Persistence.Entities;

/// <summary>EF row for dbo.NodeRegistrations.</summary>
internal sealed class DbNodeRegistration
{
    public Guid Id { get; set; }

    public long NodeNum { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public byte[] PublicKey { get; set; } = [];

    public string LongName { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;

    public byte[]? CodeHash { get; set; }

    public DateTimeOffset? CodeExpiresAt { get; set; }

    public int FailedAttempts { get; set; }

    public Guid? VerificationMessageId { get; set; }

    public DateTimeOffset ClaimedAt { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
