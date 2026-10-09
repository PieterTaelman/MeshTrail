namespace Meshtrail.Core.Contracts.Mesh;

/// <summary>
/// A text message. Direction: Inbound | Outbound. Kind: Text | Verification (text of verification messages is never
/// returned). Status: Queued | Sent | Acked | Failed | Received. ToNodeNum null = channel broadcast.
/// PeerNodeNum = the other node of a direct-message conversation. GatewayNodeNum = the gateway it went out through
/// (or first arrived through; null for team messages we sent). TeamId = team chat. Text and ChannelName come from the
/// radio (untrusted): render as text.
/// </summary>
public sealed record MessageDto(
    Guid Id,
    string Direction,
    string Kind,
    int ChannelIndex,
    string? ChannelName,
    uint? GatewayNodeNum,
    uint? FromNodeNum,
    string? FromNodeId,
    uint? ToNodeNum,
    string? ToNodeId,
    uint? PeerNodeNum,
    string Text,
    string Status,
    string? FailureReason,
    double? Snr,
    int? Rssi,
    int? HopsAway,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? SentAt,
    DateTimeOffset? AckedAt,
    Guid? TeamId);

/// <summary>
/// Query-string parameters of GET messages: exactly one of Node (the direct-message conversation with that node, both
/// directions) or Team (that team's chat). Newest first; Page is 1-based. There is no worldwide channel.
/// </summary>
public sealed record MessageListRequest
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;

    public uint? Node { get; init; }

    public Guid? Team { get; init; }
}

/// <summary>
/// Exactly one of ToNodeNum (a direct message, through the gateway that heard the node best) or TeamId (team chat,
/// through every online gateway that carries the team's channel). Text: at most 200 bytes UTF-8.
/// </summary>
public sealed record SendMessageRequest(uint? ToNodeNum, Guid? TeamId, string Text);

/// <summary>Body of POST registrations/from-contact-url: the link inside the Meshtastic app's "share contact" QR code.</summary>
public sealed record RegisterFromContactUrlRequest(string Url);

/// <summary>Body of POST registrations/{id}/verify: the 6-digit code shown on the node.</summary>
public sealed record VerifyRegistrationRequest(string Code);

/// <summary>
/// A node registration. Status: Claimed | Verified | Revoked. VerificationMessageStatus tells whether the direct
/// message with the code reached the node (Queued | Sent | Acked | Failed).
/// </summary>
public sealed record RegistrationDto(
    Guid Id,
    uint NodeNum,
    string NodeId,
    string LongName,
    string ShortName,
    string Status,
    string UserName,
    DateTimeOffset ClaimedAt,
    DateTimeOffset? CodeExpiresAt,
    int AttemptsLeft,
    DateTimeOffset? VerifiedAt,
    string? RevokedReason,
    Guid? VerificationMessageId,
    string? VerificationMessageStatus);
