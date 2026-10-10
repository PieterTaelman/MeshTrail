namespace Meshtrail.Core.Infrastructure.Persistence.Entities;

/// <summary>EF row for dbo.UserAccounts.</summary>
internal sealed class DbUserAccount
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset? EmailConfirmedAt { get; set; }

    public byte[]? ConfirmationTokenHash { get; set; }

    public DateTimeOffset? ConfirmationExpiresAt { get; set; }

    public byte[]? ResetTokenHash { get; set; }

    public DateTimeOffset? ResetExpiresAt { get; set; }

    public int FailedSignIns { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
