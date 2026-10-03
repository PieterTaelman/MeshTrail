using Meshtrail.Core.Domain.Common;

namespace Meshtrail.Core.Domain.Mesh;

public enum GatewayStatus
{
    /// <summary>Trying to open the connection.</summary>
    Connecting,

    /// <summary>Connected: packets flow both ways.</summary>
    Online,

    /// <summary>Not connected; LastError says why. The worker retries with growing pauses.</summary>
    Offline,
}

/// <summary>
/// The node that connects Meshtrail to the mesh. Its state is written by the gateway worker and shown in the
/// top bar. Key "primary" today; the key leaves room for more gateways later.
/// </summary>
public sealed class MeshGateway
{
    public const string PrimaryKey = "primary";
    public const int KeyMaxLength = 50;
    public const int ModeMaxLength = 20;
    public const int LastErrorMaxLength = 500;
    public const int FirmwareVersionMaxLength = 50;

    private MeshGateway()
    {
    }

    public string GatewayKey { get; private set; } = PrimaryKey;

    /// <summary>"Tcp" or "Simulated".</summary>
    public string Mode { get; private set; } = string.Empty;

    public GatewayStatus Status { get; private set; }

    public DateTimeOffset StatusChangedAt { get; private set; }

    public DateTimeOffset? LastConnectedAt { get; private set; }

    public string? LastError { get; private set; }

    /// <summary>The gateway's own node number, known once it is connected.</summary>
    public uint? NodeNum { get; private set; }

    public string? FirmwareVersion { get; private set; }

    public static MeshGateway Register(string gatewayKey, string mode, DateTimeOffset now) => new()
    {
        GatewayKey = gatewayKey,
        Mode = Truncate(mode, ModeMaxLength) ?? string.Empty,
        Status = GatewayStatus.Offline,
        StatusChangedAt = now,
    };

    public static MeshGateway Rehydrate(
        string gatewayKey,
        string mode,
        GatewayStatus status,
        DateTimeOffset statusChangedAt,
        DateTimeOffset? lastConnectedAt,
        string? lastError,
        uint? nodeNum,
        string? firmwareVersion) => new()
    {
        GatewayKey = gatewayKey,
        Mode = mode,
        Status = status,
        StatusChangedAt = statusChangedAt,
        LastConnectedAt = lastConnectedAt,
        LastError = lastError,
        NodeNum = nodeNum,
        FirmwareVersion = firmwareVersion,
    };

    /// <summary>Rule: we only accept something to send while the gateway is connected, so users get a clear error.</summary>
    public static void EnsureCanSend(MeshGateway? gateway)
    {
        if (gateway?.Status != GatewayStatus.Online)
        {
            throw new DomainException("The gateway is not connected, so nothing can be sent to the mesh right now.");
        }
    }

    /// <summary>Applies a new connection state. Returns false when nothing changed (so nobody gets notified).</summary>
    public bool ChangeStatus(GatewayStatus status, string mode, string? error, DateTimeOffset now)
    {
        var newMode = Truncate(mode, ModeMaxLength) ?? Mode;
        var newError = status == GatewayStatus.Online ? null : Truncate(error, LastErrorMaxLength) ?? LastError;
        if (status == Status && newMode == Mode && newError == LastError)
        {
            return false;
        }

        if (status != Status)
        {
            StatusChangedAt = now;
        }

        if (status == GatewayStatus.Online)
        {
            LastConnectedAt = now;
        }

        Status = status;
        Mode = newMode;
        LastError = newError;
        return true;
    }

    /// <summary>Stores what the node told us about itself. Returns false when nothing changed.</summary>
    public bool Identify(uint? nodeNum, string? firmwareVersion)
    {
        var newNodeNum = nodeNum ?? NodeNum;
        var newFirmware = UntrustedText.Clean(firmwareVersion, FirmwareVersionMaxLength) ?? FirmwareVersion;
        if (newNodeNum == NodeNum && newFirmware == FirmwareVersion)
        {
            return false;
        }

        NodeNum = newNodeNum;
        FirmwareVersion = newFirmware;
        return true;
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length > maxLength ? value[..maxLength] : value;
}
