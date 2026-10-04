namespace Meshtrail.Core.Infrastructure.Persistence.Entities;

/// <summary>EF row for dbo.MeshMessages.</summary>
internal sealed class DbMeshMessage
{
    public Guid Id { get; set; }

    public string Direction { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public int ChannelIndex { get; set; }

    public long? FromNodeNum { get; set; }

    public long? ToNodeNum { get; set; }

    public string Text { get; set; } = string.Empty;

    public long PacketId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? FailureReason { get; set; }

    public double? Snr { get; set; }

    public int? Rssi { get; set; }

    public int? HopsAway { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public DateTimeOffset? AckedAt { get; set; }
}
