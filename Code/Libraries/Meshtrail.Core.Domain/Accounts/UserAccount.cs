using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Domain.Accounts;

/// <summary>Outcome of a sign-in attempt. A wrong password is not an exception: the attempt must be saved.</summary>
public enum SignInResult
{
    Ok,
    WrongPassword,

    /// <summary>The email address was never confirmed.</summary>
    NotConfirmed,

    /// <summary>Too many wrong passwords in a row: try again later.</summary>
    Locked,
}

/// <summary>
/// A person's Meshtrail account: email, name and a password (stored only as a hash). The email address must be
/// confirmed with the link we mail before the account can sign in. Tokens in mails are random; only their SHA-256 is
/// stored, so a database leak does not give working links.
/// </summary>
public sealed class UserAccount
{
    public const int EmailMaxLength = 256;
    public const int NameMaxLength = 100;
    public const int PasswordMinLength = 10;
    public const int MaxFailedSignIns = 5;

    public static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan ResetLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private UserAccount()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Lower-case, so "Pieter@X.be" and "pieter@x.be" are the same account.</summary>
    public string Email { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public DateTimeOffset? EmailConfirmedAt { get; private set; }

    public byte[]? ConfirmationTokenHash { get; private set; }

    public DateTimeOffset? ConfirmationExpiresAt { get; private set; }

    public byte[]? ResetTokenHash { get; private set; }

    public DateTimeOffset? ResetExpiresAt { get; private set; }

    public int FailedSignIns { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public string DisplayName => $"{FirstName} {LastName}";

    public bool IsConfirmed => EmailConfirmedAt is not null;

    /// <summary>The same normalisation everywhere, so look-ups by email always match.</summary>
    public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <param name="passwordHash">Hashed by the caller (the hashing algorithm is infrastructure).</param>
    /// <param name="confirmationToken">Random token that goes into the confirmation link.</param>
    public static UserAccount Register(string email, string firstName, string lastName, string passwordHash, string confirmationToken, DateTimeOffset now)
    {
        var account = new UserAccount
        {
            Id = Guid.CreateVersion7(now),
            Email = CheckEmail(email),
            PasswordHash = passwordHash,
            CreatedAt = now,
        };
        account.Rename(firstName, lastName);
        account.IssueConfirmation(confirmationToken, now);
        return account;
    }

    public static UserAccount Rehydrate(
        Guid id,
        string email,
        string firstName,
        string lastName,
        string passwordHash,
        DateTimeOffset? emailConfirmedAt,
        byte[]? confirmationTokenHash,
        DateTimeOffset? confirmationExpiresAt,
        byte[]? resetTokenHash,
        DateTimeOffset? resetExpiresAt,
        int failedSignIns,
        DateTimeOffset? lockedUntil,
        DateTimeOffset createdAt,
        byte[] rowVersion) => new()
    {
        Id = id,
        Email = email,
        FirstName = firstName,
        LastName = lastName,
        PasswordHash = passwordHash,
        EmailConfirmedAt = emailConfirmedAt,
        ConfirmationTokenHash = confirmationTokenHash,
        ConfirmationExpiresAt = confirmationExpiresAt,
        ResetTokenHash = resetTokenHash,
        ResetExpiresAt = resetExpiresAt,
        FailedSignIns = failedSignIns,
        LockedUntil = lockedUntil,
        CreatedAt = createdAt,
        RowVersion = rowVersion,
    };

    public static string CheckEmail(string? email)
    {
        var normalized = NormalizeEmail(email);
        if (normalized.Length is 0 or > EmailMaxLength || !MailAddress.TryCreate(normalized, out var parsed) || parsed.Address != normalized)
        {
            throw new DomainException("Enter a valid email address.");
        }

        return normalized;
    }

    public void Rename(string firstName, string lastName)
    {
        var first = UntrustedText.Clean(firstName, NameMaxLength);
        var last = UntrustedText.Clean(lastName, NameMaxLength);
        if (first is null || last is null)
        {
            throw new DomainException($"First and last name are required (at most {NameMaxLength} characters).");
        }

        FirstName = first;
        LastName = last;
    }

    /// <summary>A new confirmation link (the old one stops working).</summary>
    public void IssueConfirmation(string token, DateTimeOffset now)
    {
        ConfirmationTokenHash = Hash(token);
        ConfirmationExpiresAt = now + ConfirmationLifetime;
    }

    /// <summary>Checks the token from the link. Returns false for a wrong or expired token. Confirming twice is fine.</summary>
    public bool ConfirmEmail(string? token, DateTimeOffset now)
    {
        if (IsConfirmed)
        {
            return true;
        }

        if (!Matches(token, ConfirmationTokenHash) || now > ConfirmationExpiresAt)
        {
            return false;
        }

        EmailConfirmedAt = now;
        ConfirmationTokenHash = null;
        ConfirmationExpiresAt = null;
        return true;
    }

    /// <summary>
    /// Records a sign-in attempt. The caller already checked the password (<paramref name="passwordOk"/>).
    /// Five wrong passwords in a row lock the account for 15 minutes.
    /// </summary>
    public SignInResult SignIn(bool passwordOk, DateTimeOffset now)
    {
        if (LockedUntil is { } until && until > now)
        {
            return SignInResult.Locked;
        }

        if (!passwordOk)
        {
            FailedSignIns++;
            if (FailedSignIns >= MaxFailedSignIns)
            {
                FailedSignIns = 0;
                LockedUntil = now + LockoutDuration;
                return SignInResult.Locked;
            }

            return SignInResult.WrongPassword;
        }

        FailedSignIns = 0;
        LockedUntil = null;
        return IsConfirmed ? SignInResult.Ok : SignInResult.NotConfirmed;
    }

    /// <summary>A password-reset link (the old one stops working).</summary>
    public void RequestReset(string token, DateTimeOffset now)
    {
        ResetTokenHash = Hash(token);
        ResetExpiresAt = now + ResetLifetime;
    }

    /// <summary>Sets a new password when the reset token is right and not expired. The link also proves the email address.</summary>
    public bool ResetPassword(string? token, string newPasswordHash, DateTimeOffset now)
    {
        if (!Matches(token, ResetTokenHash) || now > ResetExpiresAt)
        {
            return false;
        }

        PasswordHash = newPasswordHash;
        ResetTokenHash = null;
        ResetExpiresAt = null;
        FailedSignIns = 0;
        LockedUntil = null;
        EmailConfirmedAt ??= now;
        return true;
    }

    /// <summary>A newer password hash format (the hasher upgrades old hashes on sign-in).</summary>
    public void UpgradePasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void SyncRowVersion(byte[] rowVersion) => RowVersion = rowVersion;

    private bool Matches(string? token, byte[]? hash) =>
        hash is not null && !string.IsNullOrEmpty(token) && CryptographicOperations.FixedTimeEquals(Hash(token), hash);

    // The account id is part of the hash, so the same token for two accounts gives different hashes.
    private byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes($"{Id:N}:{token}"));
}
