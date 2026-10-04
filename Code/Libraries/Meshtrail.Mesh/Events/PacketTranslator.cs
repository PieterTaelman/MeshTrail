using Google.Protobuf;
using Meshtastic.Protobufs;

namespace Meshtrail.Mesh.Events;

/// <summary>
/// Turns protobuf messages from the gateway into <see cref="MeshEvent"/>s. Keeps one piece of state: the gateway's
/// own node number, so packets the gateway itself sends are not reported with a fake 0 dB signal.
/// Use one instance per connection.
/// </summary>
public sealed class PacketTranslator
{
    /// <summary>The protocol uses -128 (INT8_MIN) for "SNR unknown" in traceroutes.</summary>
    private const int UnknownSnr = sbyte.MinValue;

    public uint? GatewayNodeNum { get; private set; }

    public IReadOnlyList<MeshEvent> Translate(FromRadio message, DateTimeOffset receivedAt) => message.PayloadVariantCase switch
    {
        FromRadio.PayloadVariantOneofCase.MyInfo => [IdentifyGateway(message.MyInfo, receivedAt)],
        FromRadio.PayloadVariantOneofCase.Metadata => [new GatewayInfoReceived(receivedAt, null, Text(message.Metadata.FirmwareVersion))],
        FromRadio.PayloadVariantOneofCase.NodeInfo when message.NodeInfo.Num != 0 => [ToNodeInfo(message.NodeInfo, receivedAt)],
        FromRadio.PayloadVariantOneofCase.ConfigCompleteId => [new ConfigCompleted(receivedAt)],
        FromRadio.PayloadVariantOneofCase.Packet when message.Packet.From != 0 => TranslatePacket(message.Packet, receivedAt),
        // Config, channels (with their keys), module config, log lines …: nothing the application needs.
        _ => [],
    };

    private GatewayInfoReceived IdentifyGateway(MyNodeInfo info, DateTimeOffset receivedAt)
    {
        GatewayNodeNum = info.MyNodeNum;
        return new GatewayInfoReceived(receivedAt, info.MyNodeNum, null);
    }

    private NodeInfoReceived ToNodeInfo(NodeInfo node, DateTimeOffset receivedAt)
    {
        var isGateway = node.Num == GatewayNodeNum;
        return new NodeInfoReceived(
            receivedAt,
            node.Num,
            node.User is null ? null : ToUser(node.User),
            node.Position is null ? null : ToPosition(node.Position),
            node.DeviceMetrics is null ? null : ToTelemetry(node.DeviceMetrics),
            node.LastHeard == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(node.LastHeard),
            isGateway || node.Snr == 0 ? null : Math.Round(node.Snr, 2),
            node.HasHopsAway ? (int)node.HopsAway : null);
    }

    private List<MeshEvent> TranslatePacket(MeshPacket packet, DateTimeOffset receivedAt)
    {
        var heard = ToHeard(packet, receivedAt);

        // Even an encrypted packet we cannot read proves the node is alive and in range.
        var events = new List<MeshEvent> { heard };

        if (packet.Decoded is not { } data)
        {
            return events;
        }

        try
        {
            if (TranslatePayload(packet, data, heard) is { } payloadEvent)
            {
                events.Add(payloadEvent);
            }
        }
        catch (InvalidProtocolBufferException)
        {
            // A malformed payload from some node must not break the gateway; we still know the node was heard.
        }

        return events;
    }

    private NodeHeard ToHeard(MeshPacket packet, DateTimeOffset receivedAt)
    {
        var fromGateway = packet.From == GatewayNodeNum;
        return new NodeHeard(
            receivedAt,
            packet.From,
            fromGateway ? null : Math.Round(packet.RxSnr, 2),
            fromGateway || !packet.HasRxRssi || packet.RxRssi == 0 ? null : packet.RxRssi,
            HopsAway(packet));
    }

    private static MeshEvent? TranslatePayload(MeshPacket packet, Data data, NodeHeard heard) => data.Portnum switch
    {
        PortNum.TextMessageApp => new TextReceived(
            heard.ReceivedAt, packet.From, packet.To, (int)packet.Channel, data.Payload.ToStringUtf8(), packet.Id, heard.Snr, heard.Rssi, heard.HopsAway),
        // Only reports about our own packets (request_id set) matter.
        PortNum.RoutingApp when data.RequestId != 0 =>
            new RoutingReceived(heard.ReceivedAt, packet.From, data.RequestId, Routing.Parser.ParseFrom(data.Payload).ErrorReason.ToString()),
        PortNum.NodeinfoApp => new NodeUserReceived(heard.ReceivedAt, packet.From, ToUser(User.Parser.ParseFrom(data.Payload))),
        PortNum.PositionApp when ToPosition(Position.Parser.ParseFrom(data.Payload)) is { } position =>
            new PositionReceived(heard.ReceivedAt, packet.From, position),
        PortNum.TelemetryApp when Telemetry.Parser.ParseFrom(data.Payload) is { DeviceMetrics: { } metrics } =>
            new TelemetryReceived(heard.ReceivedAt, packet.From, ToTelemetry(metrics)),
        // Only answers (request_id set) are results; a traceroute request passing through us is not.
        PortNum.TracerouteApp when data.RequestId != 0 => ToTraceroute(packet, data, heard.ReceivedAt),
        _ => null,
    };

    private static TracerouteReceived ToTraceroute(MeshPacket packet, Data data, DateTimeOffset receivedAt)
    {
        var route = RouteDiscovery.Parser.ParseFrom(data.Payload);
        return new TracerouteReceived(
            receivedAt,
            packet.From,
            data.RequestId,
            [.. route.Route],
            [.. route.SnrTowards.Select(ToSnr)],
            [.. route.RouteBack],
            [.. route.SnrBack.Select(ToSnr)]);
    }

    private static double? ToSnr(int quarterDb) => quarterDb == UnknownSnr ? null : quarterDb / 4.0;

    private static int? HopsAway(MeshPacket packet) =>
        packet.HopStart > 0 && packet.HopStart >= packet.HopLimit ? (int)(packet.HopStart - packet.HopLimit) : null;

    private static NodeUser ToUser(User user) => new(
        Text(user.LongName),
        Text(user.ShortName),
        user.HwModel.ToString(),
        user.Role.ToString(),
        user.PublicKey.IsEmpty ? null : user.PublicKey.ToByteArray());

    /// <summary>Returns null when the node sent no usable fix (missing, 0/0 or out of range).</summary>
    private static PositionReport? ToPosition(Position position)
    {
        if (!position.HasLatitudeI || !position.HasLongitudeI || (position.LatitudeI == 0 && position.LongitudeI == 0))
        {
            return null;
        }

        var latitude = position.LatitudeI / 1e7;
        var longitude = position.LongitudeI / 1e7;
        if (Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180)
        {
            return null;
        }

        return new PositionReport(
            latitude,
            longitude,
            position.HasAltitude ? position.Altitude : null,
            position.Time == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(position.Time),
            (int)position.PrecisionBits);
    }

    private static DeviceTelemetry ToTelemetry(DeviceMetrics metrics) => new(
        metrics.HasBatteryLevel ? (int)metrics.BatteryLevel : null,
        metrics.HasVoltage && metrics.Voltage > 0 ? Math.Round(metrics.Voltage, 2) : null);

    private static string? Text(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
