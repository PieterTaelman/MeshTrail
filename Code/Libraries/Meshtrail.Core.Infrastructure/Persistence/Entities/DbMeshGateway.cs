namespace Meshtrail.Core.Infrastructure.Persistence.Entities;

/// <summary>EF row for dbo.MeshGateways.</summary>
internal sealed class DbMeshGateway
{
    public string GatewayKey { get; set; } = string.Empty;

    public string Mode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset StatusChangedAt { get; set; }

    public DateTimeOffset? LastConnectedAt { get; set; }

    public string? LastError { get; set; }

    public long? NodeNum { get; set; }

    public string? FirmwareVersion { get; set; }
}
