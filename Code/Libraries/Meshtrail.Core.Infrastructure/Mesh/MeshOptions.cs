namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>Settings from Meshtastic:Outbound.</summary>
public sealed class MeshOutboundOptions
{
    public const string SectionName = "Meshtastic:Outbound";

    /// <summary>Minimum pause between two packets we put on the air (EU868 duty cycle). 0 = no limit (tests only).</summary>
    public TimeSpan MinInterval { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>Settings from Meshtastic:Retention.</summary>
public sealed class MeshRetentionOptions
{
    public const string SectionName = "Meshtastic:Retention";

    /// <summary>Position history older than this many days is deleted by the NodePositionRetention job.</summary>
    public int PositionDays { get; set; } = 30;
}
