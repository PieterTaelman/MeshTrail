using System.Globalization;

namespace Meshtrail.Core.Domain.Mesh;

/// <summary>
/// A Meshtastic device the gateway has heard of. Created the first time we see it ("discovered") and then kept up
/// to date from the radio. Everything it holds came from the radio, so text is cleaned and numbers are clamped.
/// </summary>
public sealed class MeshNode
{
    public const int NodeIdLength = 9;
    public const int LongNameMaxLength = 40;
    public const int ShortNameMaxLength = 10;
    public const int HardwareModelMaxLength = 50;
    public const int RoleMaxLength = 30;
    public const int PublicKeyLength = 32;
    public const int SourceMaxLength = 20;

    /// <summary>Battery level the firmware reports when the device runs on USB/external power.</summary>
    public const int ExternalPowerLevel = 101;

    /// <summary>The only source today; later layers (trackers, feeds) will use other values.</summary>
    public const string MeshSource = "mesh";

    /// <summary>A node counts as online when we heard from it this recently.</summary>
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(15);

    /// <summary>Clocks on small devices drift; a "heard" time further ahead than this is replaced by our own time.</summary>
    private static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(1);

    private MeshNode()
    {
    }

    /// <summary>uint32 number the radio uses as the node's address.</summary>
    public uint NodeNum { get; private set; }

    /// <summary>How people write the number: "!" + 8 hex digits, e.g. !f115aaec.</summary>
    public string NodeId { get; private set; } = string.Empty;

    public string LongName { get; private set; } = string.Empty;

    public string ShortName { get; private set; } = string.Empty;

    public string? HardwareModel { get; private set; }

    public string? Role { get; private set; }

    /// <summary>The node's public key (32 bytes). Public by design; the firmware uses it to encrypt direct messages.</summary>
    public byte[]? PublicKey { get; private set; }

    public string Source { get; private set; } = MeshSource;

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset? LastHeardAt { get; private set; }

    /// <summary>Signal-to-noise ratio (dB) of the last packet our gateway received directly from it.</summary>
    public double? Snr { get; private set; }

    public int? Rssi { get; private set; }

    public int? HopsAway { get; private set; }

    /// <summary>0–100 %, or <see cref="ExternalPowerLevel"/> when on external power.</summary>
    public int? BatteryLevel { get; private set; }

    public double? Voltage { get; private set; }

    public GeoPosition? LastPosition { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsExternalPower => BatteryLevel >= ExternalPowerLevel;

    public static string FormatNodeId(uint nodeNum) => string.Create(CultureInfo.InvariantCulture, $"!{nodeNum:x8}");

    /// <summary>First sighting. Names default to what the firmware shows before a node sends its user info.</summary>
    public static MeshNode Discover(uint nodeNum, DateTimeOffset now)
    {
        var nodeId = FormatNodeId(nodeNum);
        var suffix = nodeId[^4..];
        return new MeshNode
        {
            NodeNum = nodeNum,
            NodeId = nodeId,
            LongName = $"Meshtastic {suffix}",
            ShortName = suffix,
            Source = MeshSource,
            FirstSeenAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>Rebuilds a node from stored values. Only the persistence layer should call this.</summary>
    public static MeshNode Rehydrate(
        uint nodeNum,
        string longName,
        string shortName,
        string? hardwareModel,
        string? role,
        byte[]? publicKey,
        string source,
        DateTimeOffset firstSeenAt,
        DateTimeOffset? lastHeardAt,
        double? snr,
        int? rssi,
        int? hopsAway,
        int? batteryLevel,
        double? voltage,
        GeoPosition? lastPosition,
        DateTimeOffset updatedAt) => new()
    {
        NodeNum = nodeNum,
        NodeId = FormatNodeId(nodeNum),
        LongName = longName,
        ShortName = shortName,
        HardwareModel = hardwareModel,
        Role = role,
        PublicKey = publicKey,
        Source = source,
        FirstSeenAt = firstSeenAt,
        LastHeardAt = lastHeardAt,
        Snr = snr,
        Rssi = rssi,
        HopsAway = hopsAway,
        BatteryLevel = batteryLevel,
        Voltage = voltage,
        LastPosition = lastPosition,
        UpdatedAt = updatedAt,
    };

    /// <summary>Applies the user info a node broadcasts. Missing values keep what we had.</summary>
    public void ApplyUser(string? longName, string? shortName, string? hardwareModel, string? role, byte[]? publicKey, DateTimeOffset now)
    {
        LongName = UntrustedText.Clean(longName, LongNameMaxLength) ?? LongName;
        ShortName = UntrustedText.Clean(shortName, ShortNameMaxLength) ?? ShortName;
        HardwareModel = UntrustedText.Clean(hardwareModel, HardwareModelMaxLength) ?? HardwareModel;
        Role = UntrustedText.Clean(role, RoleMaxLength) ?? Role;

        // Anything that is not exactly 32 bytes is not a valid key; ignore it rather than store junk.
        if (publicKey is { Length: PublicKeyLength })
        {
            PublicKey = publicKey;
        }

        UpdatedAt = now;
    }

    /// <summary>
    /// Records that the gateway received something from this node. Older reports (e.g. a node database entry that is
    /// staler than a packet we already processed) do not overwrite newer ones.
    /// </summary>
    public void RecordHeard(DateTimeOffset heardAt, double? snr, int? rssi, int? hopsAway, DateTimeOffset now)
    {
        if (heardAt > now + MaxClockSkew)
        {
            heardAt = now;
        }

        if (LastHeardAt is { } previous && heardAt < previous)
        {
            return;
        }

        LastHeardAt = heardAt;
        Snr = snr ?? Snr;
        Rssi = rssi ?? Rssi;
        HopsAway = hopsAway is >= 0 ? hopsAway : HopsAway;
        UpdatedAt = now;
    }

    /// <summary>Stores a new fix. Returns false (and changes nothing) when it is older than the one we have.</summary>
    public bool RecordPosition(GeoPosition position, DateTimeOffset now)
    {
        if (LastPosition is { } previous && position.Time < previous.Time)
        {
            return false;
        }

        LastPosition = position;
        UpdatedAt = now;
        return true;
    }

    public void RecordTelemetry(int? batteryLevel, double? voltage, DateTimeOffset now)
    {
        if (batteryLevel is { } level)
        {
            BatteryLevel = Math.Clamp(level, 0, ExternalPowerLevel);
        }

        if (voltage is > 0 and < 100)
        {
            Voltage = voltage;
        }

        UpdatedAt = now;
    }

    public bool IsOnline(DateTimeOffset now) => LastHeardAt is { } heardAt && now - heardAt <= OnlineWindow;
}
