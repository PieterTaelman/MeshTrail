namespace Meshtrail.Mesh.Events;

/// <summary>
/// Something the mesh told us, in plain C# instead of protobuf. <see cref="PacketTranslator"/> creates these;
/// the application turns them into commands. All values come from the radio and are untrusted.
/// </summary>
public abstract record MeshEvent(DateTimeOffset ReceivedAt);

/// <summary>Who a node says it is (from the node database or a NODEINFO packet).</summary>
public sealed record NodeUser(
    string? LongName,
    string? ShortName,
    string? HardwareModel,
    string? Role,
    byte[]? PublicKey);

/// <summary>A GPS fix. Altitude in metres, Time = when the node took the fix (if it knows).</summary>
public sealed record PositionReport(
    double Latitude,
    double Longitude,
    int? Altitude,
    DateTimeOffset? Time,
    int PrecisionBits);

/// <summary>Battery level 0–100, or 101 = running on external power.</summary>
public sealed record DeviceTelemetry(int? BatteryLevel, double? Voltage);

/// <summary>Our gateway reported its node number and/or firmware version (right after connecting).</summary>
public sealed record GatewayInfoReceived(DateTimeOffset ReceivedAt, uint? NodeNum, string? FirmwareVersion)
    : MeshEvent(ReceivedAt);

/// <summary>One entry of the gateway's node database, sent during the config dump.</summary>
public sealed record NodeInfoReceived(
    DateTimeOffset ReceivedAt,
    uint NodeNum,
    NodeUser? User,
    PositionReport? Position,
    DeviceTelemetry? Telemetry,
    DateTimeOffset? LastHeardAt,
    double? Snr,
    int? HopsAway) : MeshEvent(ReceivedAt);

/// <summary>A packet from this node reached our gateway. Snr/Rssi are null for packets from the gateway itself.</summary>
public sealed record NodeHeard(DateTimeOffset ReceivedAt, uint NodeNum, double? Snr, int? Rssi, int? HopsAway)
    : MeshEvent(ReceivedAt);

/// <summary>A node broadcast (or answered with) its user info.</summary>
public sealed record NodeUserReceived(DateTimeOffset ReceivedAt, uint NodeNum, NodeUser User) : MeshEvent(ReceivedAt);

public sealed record PositionReceived(DateTimeOffset ReceivedAt, uint NodeNum, PositionReport Position) : MeshEvent(ReceivedAt);

public sealed record TelemetryReceived(DateTimeOffset ReceivedAt, uint NodeNum, DeviceTelemetry Telemetry) : MeshEvent(ReceivedAt);

/// <summary>
/// Answer to a traceroute we sent (RequestId = our packet id). Routes list the node numbers in between;
/// SNR values are in dB (the protocol sends them ×4); null = unknown.
/// </summary>
public sealed record TracerouteReceived(
    DateTimeOffset ReceivedAt,
    uint NodeNum,
    uint RequestId,
    IReadOnlyList<uint> RouteTowards,
    IReadOnlyList<double?> SnrTowards,
    IReadOnlyList<uint> RouteBack,
    IReadOnlyList<double?> SnrBack) : MeshEvent(ReceivedAt);

/// <summary>The gateway finished sending its configuration and node database.</summary>
public sealed record ConfigCompleted(DateTimeOffset ReceivedAt) : MeshEvent(ReceivedAt);
