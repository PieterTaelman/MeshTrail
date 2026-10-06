using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Meshtrail.Mesh;
using Meshtrail.Mesh.Mqtt;
using Meshtrail.Mesh.Outbound;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meshtrail.MeshProbe;

/// <summary>
/// Spike tool: runs an MQTT broker that Meshtastic gateways connect to, prints everything they publish, and can
/// send packets back down (downlink) from a "virtual" Meshtrail node. Any login is accepted: LAN testing only.
/// </summary>
internal static class MqttProbe
{
    /// <summary>Default node number of the virtual Meshtrail node ("MTR1" in hex), used as sender of our packets.</summary>
    public const uint DefaultVirtualNodeNum = 0x4d54_5231;

    public static async Task<int> RunAsync(int port, uint virtualNodeNum, string? rootOverride, string? channelOverride, CancellationToken cancellationToken)
    {
        var options = new MeshtasticMqttBrokerOptions { Port = port };
        await using var broker = new MeshtasticMqttBroker(options, (_, _, _) => true, TimeProvider.System, NullLogger.Instance);
        await broker.StartAsync();

        var virtualId = NodeIds.Format(virtualNodeNum);
        Console.WriteLine($"MQTT broker listening on port {port}. Our virtual node: {virtualId}.");
        Console.WriteLine($"Point the node's MQTT module at one of: {string.Join(", ", LocalAddresses())} (TLS off, any user/password).");
        Console.WriteLine("Settings on the node: MQTT enabled, encryption OFF, JSON off; primary channel: uplink ON, downlink ON.");
        Console.WriteLine("Commands: send <!nodeid|all> <text>   sendjson <text>   quit");
        Console.WriteLine();

        var state = new ProbeState(rootOverride, channelOverride);
        var printing = PrintUplinksAsync(broker, state, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await Task.Run(Console.ReadLine, cancellationToken);
            if (line is null)
            {
                // No console to read from (input redirected): just keep listening until Ctrl+C.
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            if (line?.Trim() == "quit")
            {
                break;
            }

            try
            {
                await ExecuteAsync(line!.Trim(), broker, state, virtualNodeNum, virtualId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Console.WriteLine($"  ! {exception.Message}");
            }
        }

        await broker.StopAsync();
        await printing.ContinueWith(_ => { }, TaskScheduler.Default);
        return 0;
    }

    private static async Task ExecuteAsync(string line, MeshtasticMqttBroker broker, ProbeState state, uint virtualNodeNum, string virtualId, CancellationToken cancellationToken)
    {
        var parts = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        switch (parts)
        {
            case ["send", var target, var text]:
            {
                var (root, channel) = state.RequireRootAndChannel();
                var to = target == "all" ? NodeIds.Broadcast : ParseNode(target);
                var packetId = MeshPackets.NewPacketId();
                var packet = new MeshPacket
                {
                    From = virtualNodeNum,
                    To = to,
                    Id = packetId,
                    Channel = 0,
                    WantAck = true,
                    HopLimit = 3,
                    HopStart = 3,
                    Decoded = new Data { Portnum = PortNum.TextMessageApp, Payload = ByteString.CopyFromUtf8(text) },
                };
                await broker.PublishPacketAsync(root, channel, virtualId, packet, cancellationToken);
                Console.WriteLine($"  > published packet {packetId} to {MeshtasticTopic.ForEnvelope(root, channel, virtualId)} ({Encoding.UTF8.GetByteCount(text)} bytes)");
                break;
            }

            case ["sendjson", .. var rest] when rest.Length > 0:
            {
                // Firmware JSON downlink: only "sendtext" from the gateway's own node number, ESP32 with JSON enabled.
                var (root, _) = state.RequireRootAndChannel();
                var gateway = state.LastGatewayNodeNum ?? throw new InvalidOperationException("No gateway has published yet.");
                var payload = JsonSerializer.SerializeToUtf8Bytes(new { from = gateway, type = "sendtext", payload = string.Join(' ', rest) });
                var topic = $"{root}/2/json/mqtt/";
                await broker.PublishAsync(topic, payload, cancellationToken);
                Console.WriteLine($"  > published JSON to {topic}");
                break;
            }

            default:
                Console.WriteLine("  ? use: send <!nodeid|all> <text> | sendjson <text> | quit");
                break;
        }
    }

    private static async Task PrintUplinksAsync(MeshtasticMqttBroker broker, ProbeState state, CancellationToken cancellationToken)
    {
        await foreach (var uplink in broker.ReadAllAsync(cancellationToken))
        {
            var time = uplink.ReceivedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            var who = $"[{uplink.ClientId}{(uplink.UserName is { Length: > 0 } user ? $" user {user}" : string.Empty)}]";

            if (uplink.Topic is not { } topic)
            {
                Console.WriteLine($"{time} {who} OTHER    {uplink.RawTopic} ({uplink.Payload.Length} bytes)");
                continue;
            }

            state.Remember(topic);
            var where = $"root={topic.Root} ch={topic.Channel ?? "-"} gw={topic.GatewayId ?? "-"}";
            var detail = topic.Kind switch
            {
                MeshtasticTopicKind.Envelope when uplink.Envelope?.Packet is { } packet => PacketText.Packet(packet),
                MeshtasticTopicKind.Envelope => "unreadable envelope",
                MeshtasticTopicKind.Map => MapReportText(uplink.Payload),
                _ => Shorten(uplink.PayloadText),
            };
            Console.WriteLine($"{time} {who} {topic.Kind.ToString().ToUpperInvariant(),-8} {where}");
            Console.WriteLine($"         {detail}");
        }
    }

    private static string MapReportText(byte[] payload)
    {
        try
        {
            var report = MapReport.Parser.ParseFrom(payload);
            return $"MAP      \"{report.LongName}\" fw {report.FirmwareVersion} region {report.Region} preset {report.ModemPreset} at {report.LatitudeI / 1e7:F4},{report.LongitudeI / 1e7:F4} online nodes {report.NumOnlineLocalNodes}";
        }
        catch (InvalidProtocolBufferException)
        {
            return "unreadable map report";
        }
    }

    private static string Shorten(string text) => text.Length <= 300 ? text : text[..300] + "…";

    private static uint ParseNode(string value) =>
        NodeIds.TryParse(value, out var nodeNum) ? nodeNum
        : uint.TryParse(value, CultureInfo.InvariantCulture, out nodeNum) ? nodeNum
        : throw new FormatException($"'{value}' is not a node id like !f115aaec.");

    private static IEnumerable<string> LocalAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up && network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            .Select(address => address.ToString());

    /// <summary>Remembers root, channel and gateway from the last uplink, so downlinks need no extra arguments.</summary>
    private sealed class ProbeState(string? rootOverride, string? channelOverride)
    {
        private readonly string? _rootOverride = rootOverride;
        private readonly string? _channelOverride = channelOverride;
        private string? _root = rootOverride;
        private string? _channel = channelOverride;

        public uint? LastGatewayNodeNum { get; private set; }

        public void Remember(MeshtasticTopic topic)
        {
            _root = _rootOverride ?? topic.Root;
            if (topic.Kind == MeshtasticTopicKind.Envelope && topic.Channel is { } channel && channel != "PKI")
            {
                _channel = _channelOverride ?? channel;
            }

            if (NodeIds.TryParse(topic.GatewayId, out var gateway))
            {
                LastGatewayNodeNum = gateway;
            }
        }

        public (string Root, string Channel) RequireRootAndChannel() =>
            (_root ?? throw new InvalidOperationException("No gateway has published yet (or pass --root)."),
             _channel ?? throw new InvalidOperationException("No channel seen yet (or pass --channel)."));
    }
}
