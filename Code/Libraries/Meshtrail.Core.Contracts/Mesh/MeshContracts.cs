namespace Meshtrail.Core.Contracts.Mesh;

/// <summary>
/// A gateway: a node that connects the mesh around it to Meshtrail. Transport: Mqtt | Tcp | Simulated.
/// Status: Pending (waiting for its first uplink) | Online | Offline | Revoked. Name comes from the radio (untrusted).
/// LastError and MqttUserName are only filled for your own gateways (IsMine).
/// </summary>
public sealed record GatewayDto(
    Guid Id,
    uint? NodeNum,
    string? NodeId,
    string Name,
    string Transport,
    string Status,
    DateTimeOffset StatusChangedAt,
    DateTimeOffset? LastUplinkAt,
    string? LastError,
    string? FirmwareVersion,
    string? Broker,
    IReadOnlyList<string> Channels,
    bool IsMine,
    string? MqttUserName,
    DateTimeOffset CreatedAt,
    PositionDto? Position);

/// <summary>Counts for the "GATEWAYS online/total" chip (revoked and pending gateways are not counted).</summary>
public sealed record GatewaySummaryDto(int Online, int Total);

/// <summary>
/// What the node's MQTT settings need. ServerAddress null = the public address is not configured (use the address of
/// the machine running the broker). Encryption and JSON must be off; uplink and downlink on for the channel.
/// </summary>
public sealed record MqttSetupDto(string? ServerAddress, int Port, bool UseTls, string Root);

/// <summary>Answer of POST gateways: the password is shown this one time only (we keep only a hash).</summary>
public sealed record GatewayCredentialsDto(GatewayDto Gateway, string UserName, string Password, MqttSetupDto Setup);

/// <summary>The broker asks whether a gateway login is valid (internal; protected by the service key).</summary>
public sealed record AuthenticateGatewayRequest(string? ClientId, string? UserName, string? Password);

public sealed record AuthenticateGatewayResponse(bool Allowed);

/// <summary>A GPS fix. PrecisionBits 32 = exact; lower = deliberately blurred by the sender.</summary>
public sealed record PositionDto(double Latitude, double Longitude, int? Altitude, DateTimeOffset Time, int PrecisionBits);

/// <summary>
/// A mesh node. Names come from the radio (untrusted): show them as text, never as HTML.
/// BatteryLevel 101 means external power (see IsExternalPower). Snr, Rssi and HopsAway: the latest reception by
/// any gateway. IsGateway: the node is an active gateway.
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

    /// <summary>"west,south,east,north" in degrees: only nodes with a position inside (the map view). Optional.</summary>
    public string? Bbox { get; init; }

    /// <summary>"me" = only nodes registered to the current user. Optional.</summary>
    public string? Owner { get; init; }

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

/// <summary>A gateway that heard the node: when, with what signal and how many hops away. Name is untrusted text.</summary>
public sealed record HeardByDto(
    uint GatewayNodeNum,
    string GatewayNodeId,
    string GatewayName,
    bool GatewayOnline,
    DateTimeOffset LastHeardAt,
    double? Snr,
    int? Rssi,
    int? HopsAway);

/// <summary>Node detail page: the node, its latest traceroute and the gateways that heard it (best first).</summary>
public sealed record NodeDetailDto(NodeDto Node, NodeTracerouteDto? LastTraceroute, IReadOnlyList<HeardByDto> HeardBy);
