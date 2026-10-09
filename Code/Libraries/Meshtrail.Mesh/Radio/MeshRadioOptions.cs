namespace Meshtrail.Mesh.Radio;

/// <summary>Settings from Meshtastic:Tcp: a local base-station node the server reaches over WiFi (TCP API).</summary>
public sealed class MeshRadioOptions
{
    public const string SectionName = "Meshtastic:Tcp";

    /// <summary>The Meshtastic TCP API port; firmware always listens here.</summary>
    public const int DefaultPort = 4403;

    /// <summary>IP address or host name of the node. Empty = no TCP gateway. Keep it in user secrets, never in git.</summary>
    public string? Host { get; set; }

    public int Port { get; set; } = DefaultPort;

    /// <summary>The node drops a silent TCP client after ~15 minutes, so we send a heartbeat well before that.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public bool IsEnabled => !string.IsNullOrWhiteSpace(Host);
}
