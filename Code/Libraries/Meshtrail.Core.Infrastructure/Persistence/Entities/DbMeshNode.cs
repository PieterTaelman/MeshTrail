namespace Meshtrail.Core.Infrastructure.Persistence.Entities;

/// <summary>EF row for dbo.MeshNodes. NodeNum is a uint32 stored as BIGINT.</summary>
internal sealed class DbMeshNode
{
    public long NodeNum { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string LongName { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;

    public string? HardwareModel { get; set; }

    public string? Role { get; set; }

    public byte[]? PublicKey { get; set; }

    public string Source { get; set; } = string.Empty;

    public DateTimeOffset FirstSeenAt { get; set; }

    public DateTimeOffset? LastHeardAt { get; set; }

    public double? Snr { get; set; }

    public int? Rssi { get; set; }

    public int? HopsAway { get; set; }

    public int? BatteryLevel { get; set; }

    public double? Voltage { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int? Altitude { get; set; }

    public DateTimeOffset? PositionTime { get; set; }

    public int? PositionPrecision { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
