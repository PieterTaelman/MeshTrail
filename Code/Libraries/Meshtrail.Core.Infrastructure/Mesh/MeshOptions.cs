namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>Settings from Meshtastic:Outbound.</summary>
public sealed class MeshOutboundOptions
{
    public const string SectionName = "Meshtastic:Outbound";

    /// <summary>"MTR1" in ASCII: the sender number of our MQTT/simulator packets (must not be a real node's number).</summary>
    public const uint DefaultVirtualNodeNum = 0x4D54_5231;

    /// <summary>Minimum pause between two packets one gateway puts on the air (EU868 duty cycle). 0 = no limit (tests only).</summary>
    public TimeSpan MinInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>A sent message without a delivery report after this long is marked Failed (the firmware retries 3×).</summary>
    public TimeSpan AckTimeout { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>A message still queued after this long is marked Failed (its gateway stayed offline).</summary>
    public TimeSpan QueueTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Our node number on the air for MQTT and simulator gateways; replies and delivery reports come back to it.</summary>
    public uint VirtualNodeNum { get; set; } = DefaultVirtualNodeNum;

    /// <summary>Hop limit of our MQTT downlinks (Meshtastic default 3, max 7).</summary>
    public uint HopLimit { get; set; } = 3;
}

/// <summary>Settings from Meshtastic:Ingest: how incoming packets are processed.</summary>
public sealed class MeshIngestOptions
{
    public const string SectionName = "Meshtastic:Ingest";

    /// <summary>The same packet (sender + packet id) heard by several gateways within this window is processed once.</summary>
    public TimeSpan DedupeWindow { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>"Gateway G heard node N" is written at most once per this interval (the online window is 15 minutes).</summary>
    public TimeSpan HeardThrottle { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>How often a gateway's login and status are checked against the database while it sends packets.</summary>
    public TimeSpan GatewayRefresh { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Parallel workers. Packets of one node always go to the same worker, so they stay in order.</summary>
    public int Workers { get; set; } = 4;
}

/// <summary>Settings from Meshtastic:Mqtt: the API logs in to the Meshtrail broker with the service account.</summary>
public sealed class MeshMqttOptions
{
    public const string SectionName = "Meshtastic:Mqtt";

    public bool Enabled { get; set; }

    /// <summary>Name of the broker (region). Must match MqttBroker:Name of the broker service.</summary>
    public string Broker { get; set; } = "local";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1883;

    public string UserName { get; set; } = "meshtrail-api";

    /// <summary>Service password (same as MqttBroker:ServicePassword). A secret: user secrets / environment.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Root topic new gateways are told to use, and the default for downlinks.</summary>
    public string Root { get; set; } = "msh/EU_868";

    /// <summary>Channel for direct messages when a gateway has not reported a channel yet.</summary>
    public string DefaultChannel { get; set; } = "LongFast";

    /// <summary>Address gateways use to reach the broker (shown in the setup instructions). Empty = not configured.</summary>
    public string? PublicHost { get; set; }

    public int PublicPort { get; set; } = 1883;

    public bool UseTls { get; set; }

    /// <summary>Development only: uplinks from logins that are not registered become ownerless gateways.</summary>
    public bool AcceptUnregisteredGateways { get; set; }
}

/// <summary>Settings from Meshtastic:Retention.</summary>
public sealed class MeshRetentionOptions
{
    public const string SectionName = "Meshtastic:Retention";

    /// <summary>Position history older than this many days is deleted by the NodePositionRetention job.</summary>
    public int PositionDays { get; set; } = 30;

    /// <summary>Received channel messages older than this many days are deleted by the same job.</summary>
    public int InboundBroadcastDays { get; set; } = 30;
}
