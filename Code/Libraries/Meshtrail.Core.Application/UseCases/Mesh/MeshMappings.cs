using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh;

/// <summary>Domain → contract mapping for the Mesh use cases.</summary>
internal static class MeshMappings
{
    public static NodeDto ToDto(this MeshNode node, DateTimeOffset now, uint? gatewayNodeNum) => new(
        node.NodeNum,
        node.NodeId,
        node.LongName,
        node.ShortName,
        node.HardwareModel,
        node.Role,
        node.PublicKey is not null,
        node.Source,
        node.FirstSeenAt,
        node.LastHeardAt,
        node.IsOnline(now),
        node.Snr,
        node.Rssi,
        node.HopsAway,
        node.BatteryLevel,
        node.IsExternalPower,
        node.Voltage,
        node.LastPosition?.ToDto(),
        node.NodeNum == gatewayNodeNum,
        // Registration is not implemented yet, so no node is registered.
        IsRegistered: false);

    public static PositionDto ToDto(this GeoPosition position) =>
        new(position.Latitude, position.Longitude, position.Altitude, position.Time, position.PrecisionBits);

    public static GatewayStatusDto ToDto(this MeshGateway gateway) => new(
        gateway.Status.ToString(),
        gateway.Mode,
        gateway.StatusChangedAt,
        gateway.LastConnectedAt,
        gateway.LastError,
        gateway.NodeNum,
        gateway.NodeNum is { } nodeNum ? MeshNode.FormatNodeId(nodeNum) : null,
        gateway.FirmwareVersion);

    public static NodeTracerouteDto ToDto(this NodeTraceroute traceroute, DateTimeOffset now) => new(
        traceroute.Id,
        traceroute.NodeNum,
        traceroute.GetStatus(now).ToString(),
        traceroute.RequestedAt,
        traceroute.RequestedBy,
        traceroute.CompletedAt,
        ToHops(traceroute.RouteTowards, traceroute.SnrTowards),
        ToHops(traceroute.RouteBack, traceroute.SnrBack));

    private static List<RouteHopDto> ToHops(IReadOnlyList<uint> route, IReadOnlyList<double?> snr) =>
        [.. route.Select((nodeNum, index) => new RouteHopDto(nodeNum, MeshNode.FormatNodeId(nodeNum), index < snr.Count ? snr[index] : null))];
}
