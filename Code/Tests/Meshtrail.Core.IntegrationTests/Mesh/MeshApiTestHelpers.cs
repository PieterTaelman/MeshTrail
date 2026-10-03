using System.Net.Http.Json;
using System.Security.Cryptography;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.IntegrationTests.Infrastructure;

namespace Meshtrail.Core.IntegrationTests.Mesh;

internal static class MeshApiTestHelpers
{
    public const string GatewayUrl = "/api/v1/gateway";
    public const string NodesUrl = "/api/v1/nodes";
    public const string MapFeaturesUrl = "/api/v1/map/features";

    public static FakeMeshRadio Radio => AssemblySetup.Factory.Radio;

    /// <summary>A node number no other test uses, so tests sharing the database stay independent.</summary>
    public static uint UniqueNodeNum() => (uint)RandomNumberGenerator.GetInt32(0x1000_0000, int.MaxValue);

    public static string UniqueName() => $"T-{Guid.NewGuid():N}"[..20];

    public static string NodeUrl(uint nodeNum) => $"{NodesUrl}/{nodeNum}";

    private static uint Now => (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>A node database entry, as the gateway sends it right after connecting.</summary>
    public static FromRadio NodeInfo(uint nodeNum, string longName, double latitude = 50.85, double longitude = 4.35) => new()
    {
        NodeInfo = new NodeInfo
        {
            Num = nodeNum,
            User = new User { LongName = longName, ShortName = "TST", HwModel = HardwareModel.M5StackC6L },
            Position = new Position { LatitudeI = (int)(latitude * 1e7), LongitudeI = (int)(longitude * 1e7), Time = Now },
            LastHeard = Now,
            DeviceMetrics = new DeviceMetrics { BatteryLevel = 101 },
        },
    };

    /// <summary>A packet received over the air from <paramref name="from"/>.</summary>
    public static FromRadio Packet(uint from, PortNum port, IMessage payload, uint requestId = 0) => new()
    {
        Packet = new MeshPacket
        {
            From = from,
            To = FakeMeshRadio.GatewayNodeNum,
            Id = (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue),
            RxSnr = 6.25f,
            RxRssi = -70,
            HopStart = 3,
            HopLimit = 2,
            Decoded = new Data { Portnum = port, Payload = payload.ToByteString(), RequestId = requestId },
        },
    };

    public static Position PositionAt(double latitude, double longitude) =>
        new() { LatitudeI = (int)(latitude * 1e7), LongitudeI = (int)(longitude * 1e7), Time = Now, PrecisionBits = 32 };

    /// <summary>Injects a node and waits until the API has stored it.</summary>
    public static async Task<NodeDetailDto> InjectNodeAsync(HttpClient client, uint nodeNum, string longName)
    {
        Radio.Inject(NodeInfo(nodeNum, longName));
        return await EventuallyAsync(() => TryGetAsync<NodeDetailDto>(client, NodeUrl(nodeNum)), detail => detail.Node.LongName == longName);
    }

    /// <summary>Radio packets are handled in the background, so poll the API until the expected state shows up.</summary>
    public static async Task<T> EventuallyAsync<T>(Func<Task<T?>> read, Func<T, bool> done, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        T? last = default;
        while (DateTime.UtcNow < deadline)
        {
            last = await read();
            if (last is not null && done(last))
            {
                return last;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Condition not reached in time. Last value: {last}");
    }

    public static async Task<T?> TryGetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>() : default;
    }
}
