namespace Meshtrail.Mesh.Radio;

/// <summary>Which radio the gateway uses. Tcp = a real node over WiFi, Simulated = fake mesh (no hardware).</summary>
public enum MeshRadioMode
{
    Tcp,
    Simulated,
}

/// <summary>Settings from Meshtastic:Gateway in configuration.</summary>
public sealed class MeshRadioOptions
{
    public const string SectionName = "Meshtastic:Gateway";

    /// <summary>The Meshtastic TCP API port; firmware always listens here.</summary>
    public const int DefaultPort = 4403;

    public MeshRadioMode Mode { get; set; } = MeshRadioMode.Tcp;

    /// <summary>IP address or host name of the gateway node. Keep it in user secrets, never in git.</summary>
    public string? Host { get; set; }

    public int Port { get; set; } = DefaultPort;

    /// <summary>The node drops a silent TCP client after ~15 minutes, so we send a heartbeat well before that.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How often the simulator invents new traffic.</summary>
    public TimeSpan SimulatorTickInterval { get; set; } = TimeSpan.FromSeconds(15);
}
