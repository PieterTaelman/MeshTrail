namespace Meshtrail.Core.Infrastructure.Persistence.Entities;

/// <summary>EF row for dbo.MeshGateways.</summary>
internal sealed class DbMeshGateway
{
    public Guid Id { get; set; }

    public long? NodeNum { get; set; }

    public string Transport { get; set; } = string.Empty;

    public string? OwnerUserId { get; set; }

    public string? OwnerName { get; set; }

    public string? MqttUserName { get; set; }

    public byte[]? CredentialHash { get; set; }

    public string? Broker { get; set; }

    public string? MqttRoot { get; set; }

    public string? DownlinkChannel { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset StatusChangedAt { get; set; }

    public DateTimeOffset? LastUplinkAt { get; set; }

    public string? LastError { get; set; }

    public string? FirmwareVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
