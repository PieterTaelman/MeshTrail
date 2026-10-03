using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Meshtastic.Protobufs;
using Meshtrail.Mesh.Radio;

namespace Meshtrail.Core.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for the gateway node in integration tests: records everything the API sends and lets a test
/// inject packets as if they came from the mesh. Connects instantly and stays online.
/// </summary>
internal sealed class FakeMeshRadio : IMeshRadio
{
    public const uint GatewayNodeNum = 0x1a2b_3c4d;

    private readonly ConcurrentQueue<ToRadio> _sent = new();
    private Channel<FromRadio> _inbound = System.Threading.Channels.Channel.CreateUnbounded<FromRadio>();
    private int _connectCount;

    public string Description => "fake";

    public MeshRadioState State { get; private set; } = MeshRadioState.Disconnected;

    /// <summary>How often the worker connected (a reconnect makes this go up).</summary>
    public int ConnectCount => _connectCount;

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        _inbound = System.Threading.Channels.Channel.CreateUnbounded<FromRadio>();
        State = MeshRadioState.Connected;
        Interlocked.Increment(ref _connectCount);

        Inject(new FromRadio { MyInfo = new MyNodeInfo { MyNodeNum = GatewayNodeNum } });
        Inject(new FromRadio { Metadata = new DeviceMetadata { FirmwareVersion = "2.7.26.test" } });
        Inject(new FromRadio { ConfigCompleteId = 1 });
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<FromRadio> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in _inbound.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    public Task SendAsync(ToRadio message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        State = MeshRadioState.Disconnected;
        _inbound.Writer.TryComplete();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => new(DisconnectAsync());

    /// <summary>Pretends the gateway received this from the mesh.</summary>
    public void Inject(FromRadio message) => _inbound.Writer.TryWrite(message);

    /// <summary>Waits until the API sent a packet that matches, or fails after <paramref name="timeout"/>.</summary>
    public async Task<MeshPacket> WaitForSentPacketAsync(Func<MeshPacket, bool> match, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < deadline)
        {
            var found = _sent.Select(message => message.Packet).FirstOrDefault(packet => packet is not null && match(packet));
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("The API did not send the expected packet in time.");
    }
}
