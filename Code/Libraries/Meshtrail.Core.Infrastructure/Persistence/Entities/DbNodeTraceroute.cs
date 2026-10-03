namespace Meshtrail.Core.Infrastructure.Persistence.Entities;

/// <summary>EF row for dbo.NodeTraceroutes. Routes are comma-separated strings.</summary>
internal sealed class DbNodeTraceroute
{
    public Guid Id { get; set; }

    public long NodeNum { get; set; }

    public long PacketId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; set; }

    public string RequestedBy { get; set; } = string.Empty;

    public DateTimeOffset? CompletedAt { get; set; }

    public string? RouteTowards { get; set; }

    public string? SnrTowards { get; set; }

    public string? RouteBack { get; set; }

    public string? SnrBack { get; set; }
}
