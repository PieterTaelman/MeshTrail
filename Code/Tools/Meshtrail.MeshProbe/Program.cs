using System.Globalization;
using Meshtastic.Protobufs;
using Meshtrail.Mesh;
using Meshtrail.Mesh.Radio;
using Microsoft.Extensions.Logging.Abstractions;

// Usage: dotnet run --project Code/Tools/Meshtrail.MeshProbe -- --host 192.168.1.20 [--port 4403]
//        dotnet run --project Code/Tools/Meshtrail.MeshProbe -- --simulated
// Prints the node's own info, its node database and then every packet until Ctrl+C.
// Channel keys (PSKs) are never printed.

// Dots in coordinates regardless of the PC's regional settings.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var options = new MeshRadioOptions();
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--host" when i + 1 < args.Length:
            options.Host = args[++i];
            break;
        case "--port" when i + 1 < args.Length:
            options.Port = int.Parse(args[++i], CultureInfo.InvariantCulture);
            break;
        case "--simulated":
            options.Mode = MeshRadioMode.Simulated;
            break;
    }
}

if (options.Mode == MeshRadioMode.Tcp && string.IsNullOrWhiteSpace(options.Host))
{
    Console.Error.WriteLine("Usage: Meshtrail.MeshProbe --host <node-ip> [--port 4403] | --simulated");
    return 1;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

await using IMeshRadio radio = options.Mode == MeshRadioMode.Simulated
    ? new SimulatedMeshRadio(options, TimeProvider.System)
    : new TcpMeshRadio(options, TimeProvider.System, NullLogger<TcpMeshRadio>.Instance);

Console.WriteLine($"Connecting to {radio.Description} ...");
await radio.ConnectAsync(cancellation.Token);
Console.WriteLine("Connected. Waiting for the config dump (Ctrl+C to stop).");

try
{
    await foreach (var message in radio.ReadAllAsync(cancellation.Token))
    {
        Console.WriteLine($"{DateTime.Now:HH:mm:ss} {Describe(message)}");
    }

    Console.WriteLine("The node closed the connection.");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Stopped.");
}
catch (Exception exception)
{
    Console.WriteLine($"Connection lost: {exception.Message}");
    return 2;
}

return 0;

static string Describe(FromRadio message) => message.PayloadVariantCase switch
{
    FromRadio.PayloadVariantOneofCase.MyInfo => $"MY_INFO  node {NodeIds.Format(message.MyInfo.MyNodeNum)} ({message.MyInfo.MyNodeNum}), nodedb {message.MyInfo.NodedbCount}",
    FromRadio.PayloadVariantOneofCase.Metadata => $"METADATA firmware {message.Metadata.FirmwareVersion}, hw {message.Metadata.HwModel}, wifi {message.Metadata.HasWifi}, pkc {message.Metadata.HasPKC}",
    FromRadio.PayloadVariantOneofCase.NodeInfo => DescribeNode(message.NodeInfo),
    FromRadio.PayloadVariantOneofCase.Config => $"CONFIG   {message.Config.PayloadVariantCase}",
    FromRadio.PayloadVariantOneofCase.ModuleConfig => $"MODULE   {message.ModuleConfig.PayloadVariantCase}",
    // Only index, role and name: the channel's PSK is a secret.
    FromRadio.PayloadVariantOneofCase.Channel => $"CHANNEL  #{message.Channel.Index} {message.Channel.Role} \"{message.Channel.Settings?.Name}\"",
    FromRadio.PayloadVariantOneofCase.ConfigCompleteId => $"CONFIG COMPLETE (id {message.ConfigCompleteId})",
    FromRadio.PayloadVariantOneofCase.Packet => DescribePacket(message.Packet),
    FromRadio.PayloadVariantOneofCase.QueueStatus => $"QUEUE    free {message.QueueStatus.Free}/{message.QueueStatus.Maxlen}",
    FromRadio.PayloadVariantOneofCase.Rebooted => "REBOOTED",
    FromRadio.PayloadVariantOneofCase.ClientNotification => $"NOTICE   {message.ClientNotification.Message}",
    _ => message.PayloadVariantCase.ToString().ToUpperInvariant(),
};

static string DescribeNode(NodeInfo node)
{
    var position = node.Position is { HasLatitudeI: true, HasLongitudeI: true } p
        ? $" at {p.LatitudeI / 1e7:F5},{p.LongitudeI / 1e7:F5}"
        : string.Empty;
    var lastHeard = node.LastHeard == 0 ? "never" : DateTimeOffset.FromUnixTimeSeconds(node.LastHeard).ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    return $"NODE     {NodeIds.Format(node.Num)} \"{node.User?.LongName}\" ({node.User?.ShortName}) {node.User?.HwModel}, heard {lastHeard}, hops {(node.HasHopsAway ? node.HopsAway.ToString(CultureInfo.InvariantCulture) : "?")}{position}";
}

static string DescribePacket(MeshPacket packet)
{
    var route = $"{NodeIds.Format(packet.From)} -> {(packet.To == NodeIds.Broadcast ? "all" : NodeIds.Format(packet.To))} ch{packet.Channel} snr {packet.RxSnr} rssi {packet.RxRssi}";
    if (packet.Decoded is not { } data)
    {
        return $"PACKET   {route} (encrypted, we do not have the key)";
    }

    var detail = data.Portnum switch
    {
        PortNum.TextMessageApp => $"\"{data.Payload.ToStringUtf8()}\"",
        PortNum.PositionApp when Position.Parser.ParseFrom(data.Payload) is var p && p.HasLatitudeI => $"{p.LatitudeI / 1e7:F5},{p.LongitudeI / 1e7:F5} alt {p.Altitude}",
        PortNum.TelemetryApp when Telemetry.Parser.ParseFrom(data.Payload) is { DeviceMetrics: { } m } => $"battery {m.BatteryLevel}% {m.Voltage:F2} V",
        PortNum.RoutingApp => $"routing {Routing.Parser.ParseFrom(data.Payload).ErrorReason} for request {data.RequestId}",
        PortNum.NodeinfoApp => $"user \"{User.Parser.ParseFrom(data.Payload).LongName}\"",
        _ => $"{data.Payload.Length} bytes",
    };
    return $"PACKET   {route} {data.Portnum}: {detail}";
}
