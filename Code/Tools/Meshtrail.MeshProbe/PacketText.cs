using System.Globalization;
using Meshtastic.Protobufs;
using Meshtrail.Mesh;

namespace Meshtrail.MeshProbe;

/// <summary>One-line descriptions of protocol messages for the console. Channel keys (PSKs) are never printed.</summary>
internal static class PacketText
{
    public static string Describe(FromRadio message) => message.PayloadVariantCase switch
    {
        FromRadio.PayloadVariantOneofCase.MyInfo => $"MY_INFO  node {NodeIds.Format(message.MyInfo.MyNodeNum)} ({message.MyInfo.MyNodeNum}), nodedb {message.MyInfo.NodedbCount}",
        FromRadio.PayloadVariantOneofCase.Metadata => $"METADATA firmware {message.Metadata.FirmwareVersion}, hw {message.Metadata.HwModel}, wifi {message.Metadata.HasWifi}, pkc {message.Metadata.HasPKC}",
        FromRadio.PayloadVariantOneofCase.NodeInfo => Node(message.NodeInfo),
        FromRadio.PayloadVariantOneofCase.Config => $"CONFIG   {message.Config.PayloadVariantCase}",
        FromRadio.PayloadVariantOneofCase.ModuleConfig => $"MODULE   {message.ModuleConfig.PayloadVariantCase}",
        // Only index, role and name: the channel's PSK is a secret.
        FromRadio.PayloadVariantOneofCase.Channel => $"CHANNEL  #{message.Channel.Index} {message.Channel.Role} \"{message.Channel.Settings?.Name}\"",
        FromRadio.PayloadVariantOneofCase.ConfigCompleteId => $"CONFIG COMPLETE (id {message.ConfigCompleteId})",
        FromRadio.PayloadVariantOneofCase.Packet => Packet(message.Packet),
        FromRadio.PayloadVariantOneofCase.QueueStatus => $"QUEUE    free {message.QueueStatus.Free}/{message.QueueStatus.Maxlen}",
        FromRadio.PayloadVariantOneofCase.Rebooted => "REBOOTED",
        FromRadio.PayloadVariantOneofCase.ClientNotification => $"NOTICE   {message.ClientNotification.Message}",
        _ => message.PayloadVariantCase.ToString().ToUpperInvariant(),
    };

    public static string Node(NodeInfo node)
    {
        var position = node.Position is { HasLatitudeI: true, HasLongitudeI: true } p
            ? $" at {p.LatitudeI / 1e7:F5},{p.LongitudeI / 1e7:F5}"
            : string.Empty;
        var lastHeard = node.LastHeard == 0 ? "never" : DateTimeOffset.FromUnixTimeSeconds(node.LastHeard).ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        var hops = node.HasHopsAway ? node.HopsAway.ToString(CultureInfo.InvariantCulture) : "?";
        return $"NODE     {NodeIds.Format(node.Num)} \"{node.User?.LongName}\" ({node.User?.ShortName}) {node.User?.HwModel}, heard {lastHeard}, hops {hops}{position}";
    }

    public static string Packet(MeshPacket packet)
    {
        var to = packet.To == NodeIds.Broadcast ? "all" : NodeIds.Format(packet.To);
        var route = $"{NodeIds.Format(packet.From)} -> {to} id {packet.Id} ch{packet.Channel} hops {packet.HopStart}/{packet.HopLimit} snr {packet.RxSnr} rssi {packet.RxRssi}";
        if (packet.Decoded is not { } data)
        {
            return $"PACKET   {route} (encrypted, we do not have the key)";
        }

        string detail;
        try
        {
            detail = data.Portnum switch
            {
                PortNum.TextMessageApp => $"\"{data.Payload.ToStringUtf8()}\"",
                PortNum.PositionApp when Position.Parser.ParseFrom(data.Payload) is var p && p.HasLatitudeI => $"{p.LatitudeI / 1e7:F5},{p.LongitudeI / 1e7:F5} alt {p.Altitude}",
                PortNum.TelemetryApp when Telemetry.Parser.ParseFrom(data.Payload) is { DeviceMetrics: { } m } => $"battery {m.BatteryLevel}% {m.Voltage:F2} V",
                PortNum.RoutingApp => $"routing {Routing.Parser.ParseFrom(data.Payload).ErrorReason} for request {data.RequestId}",
                PortNum.NodeinfoApp => $"user \"{User.Parser.ParseFrom(data.Payload).LongName}\"",
                _ => $"{data.Payload.Length} bytes",
            };
        }
        catch (Google.Protobuf.InvalidProtocolBufferException)
        {
            detail = "unreadable payload";
        }

        var flags = (packet.WantAck ? " want_ack" : string.Empty) + (data.RequestId != 0 ? $" request_id {data.RequestId}" : string.Empty);
        return $"PACKET   {route}{flags} {data.Portnum}: {detail}";
    }
}
