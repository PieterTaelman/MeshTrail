using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Meshtrail.Mesh.Contacts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meshtrail.Mesh.Radio;

/// <summary>
/// A fake mesh so the whole app runs without hardware: a handful of nodes around Belgium that move, report
/// battery, chat a little, acknowledge what we send and can raise an SOS on demand (<see cref="RaiseSos"/>).
/// A fake node has no screen, so the simulator logs the direct messages it receives (e.g. verification codes)
/// and each node's contact link (to try the registration flow). Development only.
/// </summary>
public sealed partial class SimulatedMeshRadio(MeshRadioOptions options, TimeProvider timeProvider, ILogger<SimulatedMeshRadio>? logger = null)
    : IMeshRadio
{
    /// <summary>Node number of the simulated gateway (looks like a real C6L id, but is not yours).</summary>
    public const uint GatewayNodeNum = 0x5101_aaec;

    private static readonly string[] ChatLines =
    [
        "Reached the hut, all good",
        "Taking a break at the viewpoint",
        "Trail is muddy after the bridge",
        "Back at the car park",
        "Anyone near the ridge?",
    ];

    private readonly ILogger _logger = logger ?? NullLogger<SimulatedMeshRadio>.Instance;
    private readonly Random _random = new();
    private readonly Lock _lock = new();
    private readonly List<SimulatedNode> _nodes =
    [
        new(0x5101_0001, "Sim Brussels", "BRU", 50.8467, 4.3525),
        new(0x5101_0002, "Sim Ghent", "GNT", 51.0543, 3.7174),
        new(0x5101_0003, "Sim Durbuy", "DRB", 50.3527, 5.4566),
        new(0x5101_0004, "Sim La Roche", "LRC", 50.1833, 5.5759),
        new(0x5101_0005, "Sim Bouillon", "BOU", 49.7934, 5.0678),
    ];

    private Channel<FromRadio>? _inbound;
    private CancellationTokenSource? _connectionCts;
    private ITimer? _ticker;

    public string Description => "simulator";

    public MeshRadioState State { get; private set; } = MeshRadioState.Disconnected;

    /// <summary>The fake nodes' numbers, e.g. to pick one in a dev endpoint.</summary>
    public IReadOnlyList<uint> NodeNums => [.. _nodes.Select(node => node.Num)];

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _inbound = System.Threading.Channels.Channel.CreateUnbounded<FromRadio>();
        State = MeshRadioState.Connected;

        // Same order a real node uses for its config dump: my info, metadata, node database, "done".
        Emit(new FromRadio { MyInfo = new MyNodeInfo { MyNodeNum = GatewayNodeNum } });
        Emit(new FromRadio { Metadata = new DeviceMetadata { FirmwareVersion = "2.8.1.simulated", HwModel = HardwareModel.M5StackC6L, HasWifi = true, HasPKC = true } });
        Emit(new FromRadio { NodeInfo = GatewayNodeInfo() });
        lock (_lock)
        {
            foreach (var node in _nodes)
            {
                var nodeInfo = ToNodeInfo(node);
                Emit(new FromRadio { NodeInfo = nodeInfo });
                LogContactLink(_logger, node.LongName, ContactUrl.Create(new SharedContact { NodeNum = node.Num, User = nodeInfo.User }));
            }
        }

        Emit(new FromRadio { ConfigCompleteId = 1 });

        _ticker = timeProvider.CreateTimer(_ => Tick(), null, options.SimulatorTickInterval, options.SimulatorTickInterval);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<FromRadio> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var inbound = _inbound ?? throw new InvalidOperationException("Call ConnectAsync first.");
        await foreach (var message in inbound.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    public Task SendAsync(ToRadio message, CancellationToken cancellationToken)
    {
        if (State != MeshRadioState.Connected)
        {
            throw new InvalidOperationException("The simulated gateway is not connected.");
        }

        if (message.Packet is { Decoded: { } data } packet)
        {
            Respond(packet, data);
        }

        return Task.CompletedTask;
    }

    /// <summary>Makes a node (random when null) send an SOS text, preceded by a fresh position.</summary>
    public uint RaiseSos(uint? nodeNum = null)
    {
        SimulatedNode node;
        lock (_lock)
        {
            node = _nodes.FirstOrDefault(candidate => candidate.Num == nodeNum) ?? _nodes[_random.Next(_nodes.Count)];
        }

        Emit(PositionPacket(node));
        Emit(TextPacket(node, NodeIds.Broadcast, "SOS - fell near the trail, ankle injury"));
        return node.Num;
    }

    public async Task DisconnectAsync()
    {
        State = MeshRadioState.Disconnected;
        if (_ticker is not null)
        {
            await _ticker.DisposeAsync();
            _ticker = null;
        }

        if (_connectionCts is not null)
        {
            await _connectionCts.CancelAsync();
            _connectionCts.Dispose();
            _connectionCts = null;
        }

        _inbound?.Writer.TryComplete();
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();

    private void Tick()
    {
        SimulatedNode node;
        int roll;
        lock (_lock)
        {
            node = _nodes[_random.Next(_nodes.Count)];
            roll = _random.Next(100);

            // Random walk of roughly 50 m, like a hiker between two position broadcasts.
            node.Latitude += (_random.NextDouble() - 0.5) * 0.001;
            node.Longitude += (_random.NextDouble() - 0.5) * 0.0015;
            node.Battery = Math.Max(5, node.Battery - (_random.Next(10) == 0 ? 1 : 0));
        }

        Emit(roll switch
        {
            < 60 => PositionPacket(node),
            < 85 => TelemetryPacket(node),
            _ => TextPacket(node, NodeIds.Broadcast, ChatLines[_random.Next(ChatLines.Length)]),
        });
    }

    private void Respond(MeshPacket packet, Data data)
    {
        var target = FindNode(packet.To);

        switch (data.Portnum)
        {
            case PortNum.TextMessageApp:
                if (packet.WantAck)
                {
                    // Broadcasts get an "implicit ack" from our own gateway; DMs an ack from the destination.
                    var ackFrom = packet.To == NodeIds.Broadcast ? GatewayNodeNum : target?.Num ?? GatewayNodeNum;
                    var error = packet.To != NodeIds.Broadcast && target is null ? Routing.Types.Error.MaxRetransmit : Routing.Types.Error.None;
                    EmitLater(TimeSpan.FromSeconds(2), RoutingPacket(ackFrom, packet.Id, error));
                }

                if (target is not null)
                {
                    LogDirectMessage(_logger, target.LongName, data.Payload.ToStringUtf8());

                    EmitLater(TimeSpan.FromSeconds(5), TextPacket(target, GatewayNodeNum, "Copy."));
                }

                break;
            case PortNum.PositionApp when target is not null:
                EmitLater(TimeSpan.FromSeconds(3), PositionPacket(target, requestId: packet.Id));
                break;
            case PortNum.TracerouteApp when target is not null:
                EmitLater(TimeSpan.FromSeconds(4), TraceroutePacket(target, packet.Id));
                break;
            case PortNum.AdminApp:
                EmitLater(TimeSpan.FromSeconds(1), RoutingPacket(GatewayNodeNum, packet.Id, Routing.Types.Error.None));
                break;
        }
    }

    private SimulatedNode? FindNode(uint nodeNum)
    {
        lock (_lock)
        {
            return _nodes.FirstOrDefault(node => node.Num == nodeNum);
        }
    }

    private void EmitLater(TimeSpan delay, FromRadio message)
    {
        var token = _connectionCts?.Token ?? CancellationToken.None;
        _ = Task.Delay(delay, timeProvider, token).ContinueWith(
            _ => Emit(message),
            token,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }

    private void Emit(FromRadio message) => _inbound?.Writer.TryWrite(message);

    private uint Now => (uint)timeProvider.GetUtcNow().ToUnixTimeSeconds();

    private FromRadio PositionPacket(SimulatedNode node, uint requestId = 0) =>
        Packet(node.Num, NodeIds.Broadcast, PortNum.PositionApp, CurrentPosition(node), requestId);

    private FromRadio TelemetryPacket(SimulatedNode node) =>
        Packet(node.Num, NodeIds.Broadcast, PortNum.TelemetryApp, new Telemetry { Time = Now, DeviceMetrics = Metrics(node) });

    private FromRadio TextPacket(SimulatedNode node, uint to, string text) =>
        Packet(node.Num, to, PortNum.TextMessageApp, ByteString.CopyFromUtf8(text));

    private FromRadio RoutingPacket(uint from, uint requestId, Routing.Types.Error error) =>
        Packet(from, GatewayNodeNum, PortNum.RoutingApp, new Routing { ErrorReason = error }, requestId);

    private FromRadio TraceroutePacket(SimulatedNode node, uint requestId)
    {
        var relay = _nodes[0].Num == node.Num ? _nodes[1].Num : _nodes[0].Num;
        var route = new RouteDiscovery { Route = { relay }, SnrTowards = { 24, 18 }, RouteBack = { relay }, SnrBack = { 20, 22 } };
        return Packet(node.Num, GatewayNodeNum, PortNum.TracerouteApp, route, requestId);
    }

    private FromRadio Packet(uint from, uint to, PortNum port, IMessage payload, uint requestId = 0) =>
        Packet(from, to, port, payload.ToByteString(), requestId);

    private FromRadio Packet(uint from, uint to, PortNum port, ByteString payload, uint requestId = 0) => new()
    {
        Packet = new MeshPacket
        {
            From = from,
            To = to,
            Id = (uint)_random.NextInt64(1, uint.MaxValue),
            RxTime = Now,
            RxSnr = (float)Math.Round(_random.NextDouble() * 15 - 5, 1),
            RxRssi = _random.Next(-120, -60),
            HopStart = 3,
            HopLimit = (uint)_random.Next(1, 4),
            Decoded = new Data { Portnum = port, Payload = payload, RequestId = requestId },
        },
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

    private NodeInfo GatewayNodeInfo() => new()
    {
        Num = GatewayNodeNum,
        User = new User { Id = NodeIds.Format(GatewayNodeNum), LongName = "Sim Gateway", ShortName = "GW", HwModel = HardwareModel.M5StackC6L },
        LastHeard = Now,
    };

    private NodeInfo ToNodeInfo(SimulatedNode node) => new()
    {
        Num = node.Num,
        User = new User
        {
            Id = NodeIds.Format(node.Num),
            LongName = node.LongName,
            ShortName = node.ShortName,
            HwModel = HardwareModel.M5StackC6L,
            Role = Config.Types.DeviceConfig.Types.Role.Client,
            PublicKey = ByteString.CopyFrom(node.PublicKey),
        },
        Position = CurrentPosition(node),
        Snr = 6.5f,
        LastHeard = Now,
        HopsAway = 1,
        DeviceMetrics = Metrics(node),
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Simulator: contact link of {Node}: {ContactUrl}")]
    private static partial void LogContactLink(ILogger logger, string node, string contactUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "Simulator: {Node} received a direct message: {Text}")]
    private static partial void LogDirectMessage(ILogger logger, string node, string text);

    private sealed class SimulatedNode(uint num, string longName, string shortName, double latitude, double longitude)
    {
        public uint Num { get; } = num;

        public string LongName { get; } = longName;

        public string ShortName { get; } = shortName;

        public double Latitude { get; set; } = latitude;

        public double Longitude { get; set; } = longitude;

        public int Altitude { get; } = 120 + (int)(num % 300);

        public int Battery { get; set; } = 90;

        /// <summary>Fake but stable 32-byte key, so registrations of simulated nodes behave like real ones.</summary>
        public byte[] PublicKey { get; } = [.. Enumerable.Range(0, 32).Select(i => (byte)(num + i))];
    }
}
