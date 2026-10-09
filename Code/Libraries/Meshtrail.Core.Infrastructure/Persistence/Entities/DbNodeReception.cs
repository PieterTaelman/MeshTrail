namespace Meshtrail.Core.Infrastructure.Persistence.Entities;

/// <summary>EF row for dbo.NodeReceptions. Node numbers are uint32 stored as BIGINT.</summary>
internal sealed class DbNodeReception
{
    public long NodeNum { get; set; }

    public long GatewayNodeNum { get; set; }

    public DateTimeOffset LastHeardAt { get; set; }

    public double? Snr { get; set; }

    public int? Rssi { get; set; }

    public int? HopsAway { get; set; }
}
