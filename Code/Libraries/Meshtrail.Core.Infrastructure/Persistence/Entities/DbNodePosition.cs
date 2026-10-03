namespace Meshtrail.Core.Infrastructure.Persistence.Entities;

/// <summary>EF row for dbo.NodePositions.</summary>
internal sealed class DbNodePosition
{
    public Guid Id { get; set; }

    public long NodeNum { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int? Altitude { get; set; }

    public DateTimeOffset? PositionTime { get; set; }

    public int Precision { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }
}
