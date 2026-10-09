using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Meshtrail.Mesh.Contacts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meshtrail.Mesh.Simulation;

/// <summary>Settings from Meshtastic:Simulator.</summary>
public sealed class SimulatedMeshOptions
{
    public const string SectionName = "Meshtastic:Simulator";

    public bool Enabled { get; set; }

    /// <summary>The first 3 gateways are around Belgium; more are spread over Europe (for load tests).</summary>
    public int GatewayCount { get; set; } = 3;

    /// <summary>Nodes around each extra gateway (gateway 4 and up).</summary>
    public int NodesPerExtraGateway { get; set; } = 5;

    /// <summary>How often the simulator invents new traffic.</summary>
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Packets per tick (raise it together with GatewayCount for a load test).</summary>
    public int EventsPerTick { get; set; } = 1;

    /// <summary>A gateway hears every node within this distance.</summary>
    public double RangeKm { get; set; } = 60;

    /// <summary>Channel name the simulated gateways report (like the MQTT topic of a real gateway).</summary>
    public string Channel { get; set; } = "LongFast";
}

/// <summary>A simulated gateway node.</summary>
public sealed record SimulatedGateway(uint NodeNum, string Name, double Latitude, double Longitude);

/// <summary>A packet one simulated gateway heard (what a real gateway would uplink over MQTT).</summary>
public sealed record SimulatedUplink(uint GatewayNodeNum, string Channel, MeshPacket Packet);

/// <summary>
/// A fake mesh with several gateways, so the whole platform runs without hardware. Nodes move, report battery and
/// chat a little; every packet reaches each gateway within range (with its own signal and hop count), so a node can
/// be heard by more than one gateway. Downlinks behave like MQTT downlinks: the gateway transmits a packet "from" our
/// virtual node, and answers come back addressed to it. A fake node has no screen, so the simulator logs the direct
/// messages it receives (e.g. verification codes) and each named node's contact link. Development and tests only.
/// </summary>
public sealed partial class SimulatedMesh : IAsyncDisposable
{
    private const double EarthRadiusKm = 6371;

    private static readonly string[] ChatLines =
    [
        "Reached the hut, all good",
        "Taking a break at the viewpoint",
        "Trail is muddy after the bridge",
        "Back at the car park",
        "Anyone near the ridge?",
    ];

    private readonly SimulatedMeshOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly Random _random = new();
    private readonly Lock _lock = new();
    private readonly List<SimulatedNode> _nodes = [];
    private readonly Channel<SimulatedUplink> _uplinks = System.Threading.Channels.Channel.CreateUnbounded<SimulatedUplink>();
    private readonly CancellationTokenSource _stopping = new();
    private ITimer? _ticker;

    public SimulatedMesh(SimulatedMeshOptions options, TimeProvider timeProvider, ILogger<SimulatedMesh>? logger = null)
    {
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger ?? (ILogger)NullLogger.Instance;
        CreateWorld();
    }

    public IReadOnlyList<SimulatedGateway> Gateways
    {
        get
        {
            lock (_lock)
            {
                return [.. _nodes.Where(node => node.IsGateway).Select(node => new SimulatedGateway(node.Num, node.LongName, node.Latitude, node.Longitude))];
            }
        }
    }

    /// <summary>The fake (non-gateway) nodes' numbers.</summary>
    public IReadOnlyList<uint> NodeNums
    {
        get
        {
            lock (_lock)
            {
                return [.. _nodes.Where(node => !node.IsGateway).Select(node => node.Num)];
            }
        }
    }

    /// <summary>Every node announces itself (names, position, battery), then random traffic starts.</summary>
    public Task StartAsync()
    {
        List<SimulatedNode> nodes;
        lock (_lock)
        {
            nodes = [.. _nodes];
        }

        foreach (var node in nodes)
        {
            Broadcast(node, NodeInfoPacket(node));
            Broadcast(node, PositionPacket(node));
            Broadcast(node, TelemetryPacket(node));
            if (node.LogContact)
            {
                LogContactLink(_logger, node.LongName, ContactUrl.Create(new SharedContact { NodeNum = node.Num, User = ToUser(node) }));
            }
        }

        _ticker = _timeProvider.CreateTimer(_ => Tick(), null, _options.TickInterval, _options.TickInterval);
        return Task.CompletedTask;
    }

    /// <summary>Everything the gateways heard, in order.</summary>
    public async IAsyncEnumerable<SimulatedUplink> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var uplink in _uplinks.Reader.ReadAllAsync(cancellationToken))
        {
            yield return uplink;
        }
    }

    /// <summary>A downlink: <paramref name="gatewayNodeNum"/> transmits the packet; nodes within its range answer.</summary>
    public Task SendAsync(uint gatewayNodeNum, MeshPacket packet, CancellationToken cancellationToken)
    {
        var gateway = Find(gatewayNodeNum) ?? throw new InvalidOperationException($"No simulated gateway {NodeIds.Format(gatewayNodeNum)}.");
        if (packet.Decoded is { } data)
        {
            Respond(gateway, packet, data);
        }

        return Task.CompletedTask;
    }

    /// <summary>Makes a node (random when null) send an SOS text, preceded by a fresh position.</summary>
    public uint RaiseSos(uint? nodeNum = null)
    {
        SimulatedNode node;
        lock (_lock)
        {
            var candidates = _nodes.Where(candidate => !candidate.IsGateway).ToList();
            node = candidates.FirstOrDefault(candidate => candidate.Num == nodeNum) ?? candidates[_random.Next(candidates.Count)];
        }

        Broadcast(node, PositionPacket(node));
        Broadcast(node, TextPacket(node, NodeIds.Broadcast, "SOS - fell near the trail, ankle injury"));
        return node.Num;
    }

    public async ValueTask DisposeAsync()
    {
        if (_ticker is not null)
        {
            await _ticker.DisposeAsync();
            _ticker = null;
        }

        await _stopping.CancelAsync();
        _stopping.Dispose();
        _uplinks.Writer.TryComplete();
    }

    private void CreateWorld()
    {
        // Three gateways around Belgium with overlapping coverage: "Sim Brussels" is heard by two of them.
        // With a smaller GatewayCount (tests) only the first ones exist.
        SimulatedNode[] belgium =
        [
            SimulatedNode.Gateway(0x5101_aaec, "Sim Gateway Leuven", "GWL", 50.88, 4.70),
            SimulatedNode.Gateway(0x5101_aaed, "Sim Gateway Ghent", "GWG", 51.00, 3.90),
            SimulatedNode.Gateway(0x5101_aaee, "Sim Gateway Ardennes", "GWA", 50.25, 5.40),
        ];
        _nodes.AddRange(belgium.Take(Math.Clamp(_options.GatewayCount, 0, belgium.Length)));
        _nodes.Add(SimulatedNode.Hiker(0x5101_0001, "Sim Brussels", "BRU", 50.8467, 4.3525, logContact: true));
        _nodes.Add(SimulatedNode.Hiker(0x5101_0002, "Sim Ghent", "GNT", 51.0543, 3.7174, logContact: true));
        _nodes.Add(SimulatedNode.Hiker(0x5101_0003, "Sim Durbuy", "DRB", 50.3527, 5.4566, logContact: true));
        _nodes.Add(SimulatedNode.Hiker(0x5101_0004, "Sim La Roche", "LRC", 50.1833, 5.5759, logContact: true));
        _nodes.Add(SimulatedNode.Hiker(0x5101_0005, "Sim Bouillon", "BOU", 49.7934, 5.0678, logContact: true));

        // Extra gateways for load tests: a fixed seed, so every run builds the same world.
        var layout = new Random(42);
        for (var g = belgium.Length; g < _options.GatewayCount; g++)
        {
            var latitude = 40 + layout.NextDouble() * 20;
            var longitude = -8 + layout.NextDouble() * 33;
            var gatewayNum = 0x5102_0000u + (uint)g;
            _nodes.Add(SimulatedNode.Gateway(gatewayNum, $"Sim Gateway {g + 1}", $"G{g + 1}", latitude, longitude));
            for (var n = 0; n < _options.NodesPerExtraGateway; n++)
            {
                var nodeNum = 0x5103_0000u + (uint)(g * 100 + n);
                _nodes.Add(SimulatedNode.Hiker(
                    nodeNum, $"Sim Hiker {g + 1}-{n + 1}", $"H{n + 1}", latitude + (layout.NextDouble() - 0.5) * 0.3, longitude + (layout.NextDouble() - 0.5) * 0.4, logContact: false));
            }
        }
    }

    private void Tick()
    {
        for (var i = 0; i < Math.Max(_options.EventsPerTick, 1); i++)
        {
            SimulatedNode node;
            int roll;
            lock (_lock)
            {
                node = _nodes[_random.Next(_nodes.Count)];
                roll = _random.Next(100);
                if (!node.IsGateway)
                {
                    // Random walk of roughly 50 m, like a hiker between two position broadcasts.
                    node.Latitude += (_random.NextDouble() - 0.5) * 0.001;
                    node.Longitude += (_random.NextDouble() - 0.5) * 0.0015;
                    node.Battery = Math.Max(5, node.Battery - (_random.Next(10) == 0 ? 1 : 0));
                }
            }

            Broadcast(node, roll switch
            {
                < 60 => PositionPacket(node),
                < 85 => TelemetryPacket(node),
                _ when node.IsGateway => TelemetryPacket(node),
                _ => TextPacket(node, NodeIds.Broadcast, ChatLines[_random.Next(ChatLines.Length)]),
            });
        }
    }

    private void Respond(SimulatedNode gateway, MeshPacket packet, Data data)
    {
        // Only nodes within the transmitting gateway's range can hear (and answer) the downlink.
        var target = Find(packet.To);
        var reachable = target is not null && DistanceKm(gateway, target) <= _options.RangeKm;
        var replyTo = packet.From;

        switch (data.Portnum)
        {
            case PortNum.TextMessageApp:
                if (packet.WantAck)
                {
                    // Broadcasts get an "implicit ack" from the gateway; DMs an ack from the destination (or an error).
                    var (ackFrom, error) = packet.To == NodeIds.Broadcast || !reachable
                        ? (gateway, packet.To == NodeIds.Broadcast ? Routing.Types.Error.None : Routing.Types.Error.MaxRetransmit)
                        : (target!, Routing.Types.Error.None);
                    Later(TimeSpan.FromSeconds(2), ackFrom, RoutingPacket(ackFrom, replyTo, packet.Id, error));
                }

                if (reachable)
                {
                    LogDirectMessage(_logger, target!.LongName, gateway.LongName, data.Payload.ToStringUtf8());
                    Later(TimeSpan.FromSeconds(5), target, TextPacket(target, replyTo, "Copy."));
                }

                break;
            case PortNum.PositionApp when reachable:
                Later(TimeSpan.FromSeconds(3), target!, PositionPacket(target!, replyTo, requestId: packet.Id));
                break;
            case PortNum.TracerouteApp when reachable:
                Later(TimeSpan.FromSeconds(4), target!, TraceroutePacket(target!, gateway, replyTo, packet.Id));
                break;
        }
    }

    private SimulatedNode? Find(uint nodeNum)
    {
        lock (_lock)
        {
            return _nodes.FirstOrDefault(node => node.Num == nodeNum);
        }
    }

    private void Later(TimeSpan delay, SimulatedNode source, MeshPacket packet)
    {
        var token = _stopping.Token;
        _ = Task.Delay(delay, _timeProvider, token).ContinueWith(
            _ => Broadcast(source, packet),
            token,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }

    /// <summary>Every gateway within range of the sender hears the packet, each with its own signal and hop count.</summary>
    private void Broadcast(SimulatedNode source, MeshPacket packet)
    {
        List<SimulatedNode> gateways;
        lock (_lock)
        {
            gateways = [.. _nodes.Where(node => node.IsGateway)];
        }

        foreach (var gateway in gateways)
        {
            var distance = gateway.Num == source.Num ? 0 : DistanceKm(gateway, source);
            if (distance > _options.RangeKm)
            {
                continue;
            }

            var heard = packet.Clone();
            var hops = distance < 15 ? 0u : distance < 35 ? 1u : 2u;
            heard.HopStart = 3;
            heard.HopLimit = 3 - hops;
            heard.RxTime = Now;
            if (gateway.Num == source.Num)
            {
                // The gateway's own packets: no radio reception.
                heard.RxSnr = 0;
                heard.RxRssi = 0;
            }
            else
            {
                heard.RxSnr = (float)Math.Round(10 - distance / 5 + (_random.NextDouble() - 0.5) * 2, 1);
                heard.RxRssi = (int)Math.Round(-60 - distance);
            }

            _uplinks.Writer.TryWrite(new SimulatedUplink(gateway.Num, _options.Channel, heard));
        }
    }

    private uint Now => (uint)_timeProvider.GetUtcNow().ToUnixTimeSeconds();

    private MeshPacket NodeInfoPacket(SimulatedNode node) => Packet(node.Num, NodeIds.Broadcast, PortNum.NodeinfoApp, ToUser(node));

    private MeshPacket PositionPacket(SimulatedNode node, uint to = NodeIds.Broadcast, uint requestId = 0) =>
        Packet(node.Num, to, PortNum.PositionApp, CurrentPosition(node), requestId);

    private MeshPacket TelemetryPacket(SimulatedNode node) =>
        Packet(node.Num, NodeIds.Broadcast, PortNum.TelemetryApp, new Telemetry { Time = Now, DeviceMetrics = Metrics(node) });

    private MeshPacket TextPacket(SimulatedNode node, uint to, string text) =>
        Packet(node.Num, to, PortNum.TextMessageApp, ByteString.CopyFromUtf8(text));

    private MeshPacket RoutingPacket(SimulatedNode from, uint to, uint requestId, Routing.Types.Error error) =>
        Packet(from.Num, to, PortNum.RoutingApp, new Routing { ErrorReason = error }, requestId);

    private MeshPacket TraceroutePacket(SimulatedNode node, SimulatedNode gateway, uint to, uint requestId)
    {
        var route = new RouteDiscovery { Route = { gateway.Num }, SnrTowards = { 24, 18 }, RouteBack = { gateway.Num }, SnrBack = { 20, 22 } };
        return Packet(node.Num, to, PortNum.TracerouteApp, route, requestId);
    }

    private MeshPacket Packet(uint from, uint to, PortNum port, IMessage payload, uint requestId = 0) => new()
    {
        From = from,
        To = to,
        Id = (uint)_random.NextInt64(1, uint.MaxValue),
        Decoded = new Data { Portnum = port, Payload = payload.ToByteString(), RequestId = requestId },
    };

    private MeshPacket Packet(uint from, uint to, PortNum port, ByteString payload) => new()
    {
        From = from,
        To = to,
        Id = (uint)_random.NextInt64(1, uint.MaxValue),
        Decoded = new Data { Portnum = port, Payload = payload },
    };

    private Position CurrentPosition(SimulatedNode node)
    {
        lock (_lock)
        {
            return new Position
            {
                LatitudeI = (int)Math.Round(node.Latitude * 1e7),
                LongitudeI = (int)Math.Round(node.Longitude * 1e7),
                Altitude = node.Altitude,
                Time = Now,
                PrecisionBits = 32,
            };
        }
    }

    private static DeviceMetrics Metrics(SimulatedNode node) => new()
    {
        BatteryLevel = (uint)node.Battery,
        Voltage = 3.3f + node.Battery / 100f,
        ChannelUtilization = 4.5f,
        AirUtilTx = 1.2f,
    };

    private static User ToUser(SimulatedNode node) => new()
    {
        Id = NodeIds.Format(node.Num),
        LongName = node.LongName,
        ShortName = node.ShortName,
        HwModel = HardwareModel.M5StackC6L,
        Role = node.IsGateway ? Config.Types.DeviceConfig.Types.Role.ClientMute : Config.Types.DeviceConfig.Types.Role.Client,
        PublicKey = ByteString.CopyFrom(node.PublicKey),
    };

    private static double DistanceKm(SimulatedNode a, SimulatedNode b)
    {
        // Haversine: good enough for "is it in range".
        var dLat = double.DegreesToRadians(b.Latitude - a.Latitude);
        var dLon = double.DegreesToRadians(b.Longitude - a.Longitude);
        var h = Math.Pow(Math.Sin(dLat / 2), 2)
            + Math.Cos(double.DegreesToRadians(a.Latitude)) * Math.Cos(double.DegreesToRadians(b.Latitude)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(h));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Simulator: contact link of {Node}: {ContactUrl}")]
    private static partial void LogContactLink(ILogger logger, string node, string contactUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "Simulator: {Node} received a direct message via {Gateway}: {Text}")]
    private static partial void LogDirectMessage(ILogger logger, string node, string gateway, string text);

    private sealed class SimulatedNode
    {
        public uint Num { get; private init; }

        public string LongName { get; private init; } = string.Empty;

        public string ShortName { get; private init; } = string.Empty;

        public bool IsGateway { get; private init; }

        public bool LogContact { get; private init; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public int Altitude => 120 + (int)(Num % 300);

        public int Battery { get; set; } = 90;

        /// <summary>Fake but stable 32-byte key, so registrations of simulated nodes behave like real ones.</summary>
        public byte[] PublicKey => [.. Enumerable.Range(0, 32).Select(i => (byte)(Num + i))];

        public static SimulatedNode Gateway(uint num, string longName, string shortName, double latitude, double longitude) =>
            new() { Num = num, LongName = longName, ShortName = shortName, IsGateway = true, Latitude = latitude, Longitude = longitude, Battery = 101 };

        public static SimulatedNode Hiker(uint num, string longName, string shortName, double latitude, double longitude, bool logContact) =>
            new() { Num = num, LongName = longName, ShortName = shortName, Latitude = latitude, Longitude = longitude, LogContact = logContact };
    }
}
