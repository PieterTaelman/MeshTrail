using System.Security.Cryptography;
using System.Text;
using Meshtrail.Core.Domain.Common;

namespace Meshtrail.Core.Domain.Mesh;

public enum RegistrationStatus
{
    /// <summary>A user says the node is theirs; waiting for the code we sent to the node.</summary>
    Claimed,

    /// <summary>The user entered the right code, so they really hold the node.</summary>
    Verified,

    /// <summary>Withdrawn, replaced, expired or locked after too many wrong codes.</summary>
    Revoked,
}

/// <summary>Outcome of entering a code. A wrong code is not an exception: the attempt must be saved.</summary>
public enum VerificationResult
{
    Verified,
    WrongCode,

    /// <summary>Too many wrong codes: the registration is revoked and the user has to start again.</summary>
    Locked,
}

/// <summary>
/// "This node belongs to this user." Created from the node's contact link (Claimed), then proven by entering the
/// 6-digit code we send to the node as a direct message (Verified). Only a hash of the code is stored.
/// </summary>
public sealed class NodeRegistration
{
    public const int CodeLength = 6;
    public const int MaxAttempts = 5;
    public const int UserMaxLength = 256;
    public const int RevokedReasonMaxLength = 200;

    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);

    private NodeRegistration()
    {
    }

    public Guid Id { get; private set; }

    public uint NodeNum { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public string UserName { get; private set; } = string.Empty;

    public RegistrationStatus Status { get; private set; }

    /// <summary>Public key from the contact link; handed to the gateway after verification.</summary>
    public byte[] PublicKey { get; private set; } = [];

    public string LongName { get; private set; } = string.Empty;

    public string ShortName { get; private set; } = string.Empty;

    public byte[]? CodeHash { get; private set; }

    public DateTimeOffset? CodeExpiresAt { get; private set; }

    public int FailedAttempts { get; private set; }

    /// <summary>The direct message that carries the code, so the UI can show whether it was delivered.</summary>
    public Guid? VerificationMessageId { get; private set; }

    public DateTimeOffset ClaimedAt { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public int AttemptsLeft => Math.Max(0, MaxAttempts - FailedAttempts);

    public bool IsActive => Status is RegistrationStatus.Claimed or RegistrationStatus.Verified;

    /// <param name="code">6 random digits; the caller generates them so tests can choose one.</param>
    /// <param name="broadcastKey">The key the node itself broadcasts, if we heard it. Must match the link's key.</param>
    public static NodeRegistration Claim(
        uint nodeNum,
        string userId,
        string userName,
        string longName,
        string shortName,
        byte[] publicKey,
        byte[]? broadcastKey,
        string code,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new DomainException("A registration must be linked to a user.");
        }

        if (publicKey.Length != MeshNode.PublicKeyLength)
        {
            throw new DomainException("The contact link has no public key. Share the contact from a node with firmware 2.5 or newer.");
        }

        // A link with someone else's key would make the gateway encrypt messages for the wrong device.
        if (broadcastKey is not null && !broadcastKey.AsSpan().SequenceEqual(publicKey))
        {
            throw new DomainException("The key in the contact link does not match the key this node broadcasts.");
        }

        var registration = new NodeRegistration
        {
            Id = Guid.CreateVersion7(now),
            NodeNum = nodeNum,
            UserId = Cut(userId),
            UserName = Cut(string.IsNullOrWhiteSpace(userName) ? userId : userName),
            Status = RegistrationStatus.Claimed,
            PublicKey = publicKey,
            LongName = UntrustedText.Clean(longName, MeshNode.LongNameMaxLength) ?? MeshNode.FormatNodeId(nodeNum),
            ShortName = UntrustedText.Clean(shortName, MeshNode.ShortNameMaxLength) ?? MeshNode.FormatNodeId(nodeNum)[^4..],
            ClaimedAt = now,
        };
        registration.IssueCode(code, now);
        return registration;
    }

    public static NodeRegistration Rehydrate(
        Guid id,
        uint nodeNum,
        string userId,
        string userName,
        RegistrationStatus status,
        byte[] publicKey,
        string longName,
        string shortName,
        byte[]? codeHash,
        DateTimeOffset? codeExpiresAt,
        int failedAttempts,
        Guid? verificationMessageId,
        DateTimeOffset claimedAt,
        DateTimeOffset? verifiedAt,
        DateTimeOffset? revokedAt,
        string? revokedReason,
        byte[] rowVersion) => new()
    {
        Id = id,
        NodeNum = nodeNum,
        UserId = userId,
        UserName = userName,
        Status = status,
        PublicKey = publicKey,
        LongName = longName,
        ShortName = shortName,
        CodeHash = codeHash,
        CodeExpiresAt = codeExpiresAt,
        FailedAttempts = failedAttempts,
        VerificationMessageId = verificationMessageId,
        ClaimedAt = claimedAt,
        VerifiedAt = verifiedAt,
        RevokedAt = revokedAt,
        RevokedReason = revokedReason,
        RowVersion = rowVersion,
    };

    /// <summary>A claim whose code ran out no longer blocks the node for others.</summary>
    public bool IsExpired(DateTimeOffset now) => Status == RegistrationStatus.Claimed && now > CodeExpiresAt;

    public void LinkVerificationMessage(Guid messageId) => VerificationMessageId = messageId;

    /// <summary>Checks a code. Wrong codes count; the fifth wrong code revokes the registration.</summary>
    public VerificationResult Verify(string code, DateTimeOffset now)
    {
        if (Status != RegistrationStatus.Claimed)
        {
            throw new DomainException($"This registration is {Status.ToString().ToLowerInvariant()}, so it cannot be verified.");
        }

        if (now > CodeExpiresAt)
        {
            throw new DomainException("The code has expired. Register the node again to get a new code.");
        }

        if (CodeHash is not null && CryptographicOperations.FixedTimeEquals(HashCode(code?.Trim() ?? string.Empty), CodeHash))
        {
            Status = RegistrationStatus.Verified;
            VerifiedAt = now;
            ClearCode();
            return VerificationResult.Verified;
        }

        FailedAttempts++;
        if (FailedAttempts < MaxAttempts)
        {
            return VerificationResult.WrongCode;
        }

        Revoke("Too many wrong codes.", now);
        return VerificationResult.Locked;
    }

    public void Revoke(string reason, DateTimeOffset now)
    {
        if (Status == RegistrationStatus.Revoked)
        {
            return;
        }

        Status = RegistrationStatus.Revoked;
        RevokedAt = now;
        RevokedReason = reason.Length > RevokedReasonMaxLength ? reason[..RevokedReasonMaxLength] : reason;
        ClearCode();
    }

    public void SyncRowVersion(byte[] rowVersion) => RowVersion = rowVersion;

    private void IssueCode(string code, DateTimeOffset now)
    {
        if (code is not { Length: CodeLength } || !code.All(char.IsAsciiDigit))
        {
            throw new DomainException($"A verification code is {CodeLength} digits.");
        }

        CodeHash = HashCode(code);
        CodeExpiresAt = now + CodeLifetime;
        FailedAttempts = 0;
    }

    private void ClearCode()
    {
        CodeHash = null;
        CodeExpiresAt = null;
    }

    // The registration id is part of the hash, so equal codes in two registrations give different hashes.
    private byte[] HashCode(string code) => SHA256.HashData(Encoding.UTF8.GetBytes($"{Id:N}:{code}"));

    private static string Cut(string value) => value.Length > UserMaxLength ? value[..UserMaxLength] : value;
}
