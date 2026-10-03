namespace Meshtrail.Core.Contracts.Mesh;

/// <summary>
/// Gateway connection as shown in the top bar. Status: Connecting | Online | Offline. Mode: Tcp | Simulated.
/// </summary>
public sealed record GatewayStatusDto(
    string Status,
    string Mode,
    DateTimeOffset StatusChangedAt,
    DateTimeOffset? LastConnectedAt,
    string? LastError,
    uint? NodeNum,
    string? NodeId,
    string? FirmwareVersion);

/// <summary>A GPS fix. PrecisionBits 32 = exact; lower = deliberately blurred by the sender.</summary>
public sealed record PositionDto(double Latitude, double Longitude, int? Altitude, DateTimeOffset Time, int PrecisionBits);

/// <summary>
/// A mesh node. Names come from the radio (untrusted): show them as text, never as HTML.
/// BatteryLevel 101 means external power (see IsExternalPower).
/// </summary>
public sealed record NodeDto(
    uint NodeNum,
    string NodeId,
    string LongName,
    string ShortName,
    string? HardwareModel,
    string? Role,
    bool HasPublicKey,
    string Source,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset? LastHeardAt,
    bool IsOnline,
    double? Snr,
    int? Rssi,
    int? HopsAway,
    int? BatteryLevel,
    bool IsExternalPower,
    double? Voltage,
    PositionDto? Position,
    bool IsGateway,
    bool IsRegistered);

/// <summary>Query-string parameters of the node list. Page is 1-based.</summary>
public sealed record NodeListRequest
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 100;

    /// <summary>Matched against long name, short name and node id (e.g. "!f115").</summary>
    public string? Search { get; init; }

    /// <summary>true = only nodes registered to a user, false = only unregistered, null = all.</summary>
    public bool? Registered { get; init; }

    /// <summary>true = only nodes heard in the last 15 minutes, false = only the others, null = all.</summary>
    public bool? Online { get; init; }
}

/// <summary>One relay on a traceroute. Snr in dB (towards that hop), null when unknown.</summary>
public sealed record RouteHopDto(uint NodeNum, string NodeId, double? Snr);

/// <summary>Status: Pending | Completed | TimedOut. Routes exclude the two end points.</summary>
public sealed record NodeTracerouteDto(
    Guid Id,
    uint NodeNum,
    string Status,
    DateTimeOffset RequestedAt,
    string RequestedBy,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<RouteHopDto> RouteTowards,
    IReadOnlyList<RouteHopDto> RouteBack);

/// <summary>Node detail page: the node plus its latest traceroute, if any.</summary>
public sealed record NodeDetailDto(NodeDto Node, NodeTracerouteDto? LastTraceroute);
