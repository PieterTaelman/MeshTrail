namespace Meshtrail.Core.Contracts.Mesh;

/// <summary>
/// A text message. Direction: Inbound | Outbound. Kind: Text | Verification (text of verification messages is never
/// returned). Status: Queued | Sent | Acked | Failed | Received. ToNodeNum null = channel broadcast.
/// PeerNodeNum = the other node of a direct-message conversation. Text from the radio is untrusted: render as text.
/// </summary>
public sealed record MessageDto(
    Guid Id,
    string Direction,
    string Kind,
    int ChannelIndex,
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
    DateTimeOffset? AckedAt);

/// <summary>Query-string parameters of GET messages: exactly one of Channel or Node. Newest first; Page is 1-based.</summary>
public sealed record MessageListRequest
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;

    /// <summary>Broadcasts on this channel (0–7).</summary>
    public int? Channel { get; init; }

    /// <summary>The direct-message conversation with this node (both directions).</summary>
    public uint? Node { get; init; }
}

/// <summary>
/// Send a text: ToNodeNum set = direct message (ChannelIndex ignored), otherwise broadcast on ChannelIndex (default 0).
/// Text: at most 200 bytes UTF-8.
/// </summary>
public sealed record SendMessageRequest(int? ChannelIndex, uint? ToNodeNum, string Text);

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
