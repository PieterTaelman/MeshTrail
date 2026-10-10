using System.Net.Http.Json;
using System.Security.Cryptography;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Infrastructure.Mesh;
using Meshtrail.Core.IntegrationTests.Infrastructure;

namespace Meshtrail.Core.IntegrationTests.Mesh;

/// <summary>A gateway a test added through the API and brought online with its first uplink.</summary>
internal sealed record TestGateway(uint NodeNum, string Login, string Owner);

internal static class MeshApiTestHelpers
{
    public const string GatewaysUrl = "/api/v1/gateways";
    public const string NodesUrl = "/api/v1/nodes";
    public const string MapFeaturesUrl = "/api/v1/map/features";

    /// <summary>Development/Testing only: act as this user instead of the default test user.</summary>
    public const string DevUserHeader = "X-Dev-User";

    /// <summary>Our sender number on the air ("MTR1"); replies and delivery reports come back to it.</summary>
    public const uint VirtualNodeNum = MeshOutboundOptions.DefaultVirtualNodeNum;

    public static FakeGatewayTransport Transport => AssemblySetup.Factory.Transport;

    /// <summary>A node number no other test uses, so tests sharing the database stay independent.</summary>
    public static uint UniqueNodeNum() => (uint)RandomNumberGenerator.GetInt32(0x1000_0000, int.MaxValue);

    public static string UniqueName() => $"T-{Guid.NewGuid():N}"[..20];

    public static string NodeUrl(uint nodeNum) => $"{NodesUrl}/{nodeNum}";

    /// <summary>A client that acts as another user (development login with the X-Dev-User header).</summary>
    public static HttpClient ClientAs(string user)
    {
        var client = AssemblySetup.Factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevUserHeader, user);
        return client;
    }

    private static uint Now => (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>A packet as a gateway uplinks it: received from <paramref name="from"/>, with this many hops.</summary>
    public static MeshPacket Packet(uint from, PortNum port, IMessage payload, uint requestId = 0, uint to = uint.MaxValue, int hops = 1) => new()
    {
        From = from,
        To = to,
        Id = (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue),
        RxSnr = 6.25f,
        RxRssi = -70,
        HopStart = 3,
        HopLimit = (uint)(3 - hops),
        Decoded = new Data { Portnum = port, Payload = payload.ToByteString(), RequestId = requestId },
    };

    public static Position PositionAt(double latitude, double longitude) =>
        new() { LatitudeI = (int)(latitude * 1e7), LongitudeI = (int)(longitude * 1e7), Time = Now, PrecisionBits = 32 };

    public static User UserInfo(string longName, byte[]? publicKey = null) => new()
    {
        LongName = longName,
        ShortName = "TST",
        HwModel = HardwareModel.M5StackC6L,
        PublicKey = publicKey is null ? ByteString.Empty : ByteString.CopyFrom(publicKey),
    };

    /// <summary>
    /// Adds a gateway through the API (as a fresh user, so the per-user limit is never reached) and sends its first
    /// uplink, which ties it to a new node. Waits until it is online. The gateway node sits at the given position.
    /// </summary>
    public static async Task<TestGateway> AddOnlineGatewayAsync(double latitude = 50.88, double longitude = 4.70)
    {
        var owner = $"owner-{UniqueName()}";
        using var client = ClientAs(owner);
        var nodeNum = UniqueNodeNum();
        var response = await client.PostAsJsonAsync(GatewaysUrl, new AddGatewayRequest(nodeNum));
        response.EnsureSuccessStatusCode();
        var credentials = (await response.Content.ReadFromJsonAsync<GatewayCredentialsDto>())!;

        var gateway = new TestGateway(nodeNum, credentials.UserName, owner);
        Transport.Inject(gateway.NodeNum, gateway.Login, Packet(gateway.NodeNum, PortNum.NodeinfoApp, UserInfo($"GW {UniqueName()}"), hops: 0));
        Transport.Inject(gateway.NodeNum, gateway.Login, Packet(gateway.NodeNum, PortNum.PositionApp, PositionAt(latitude, longitude), hops: 0));
        await EventuallyAsync(
            async () => (await TryGetAsync<List<GatewayDto>>(client, $"{GatewaysUrl}?mine=true"))?.SingleOrDefault(),
            dto => dto is { Status: "Online", Position: not null });
        return gateway;
    }

    /// <summary>The node announces itself (names, position, battery) through <paramref name="gateway"/>; waits until it is stored.</summary>
    public static async Task<NodeDetailDto> InjectNodeAsync(
        HttpClient client, TestGateway gateway, uint nodeNum, string longName, int hops = 1, double latitude = 50.85, double longitude = 4.35, byte[]? publicKey = null)
    {
        Transport.Inject(gateway.NodeNum, gateway.Login, Packet(nodeNum, PortNum.NodeinfoApp, UserInfo(longName, publicKey), hops: hops));
        Transport.Inject(gateway.NodeNum, gateway.Login, Packet(nodeNum, PortNum.PositionApp, PositionAt(latitude, longitude), hops: hops));
        Transport.Inject(gateway.NodeNum, gateway.Login, Packet(nodeNum, PortNum.TelemetryApp, new Telemetry { DeviceMetrics = new DeviceMetrics { BatteryLevel = 101 } }, hops: hops));
        return await EventuallyAsync(
            () => TryGetAsync<NodeDetailDto>(client, NodeUrl(nodeNum)),
            detail => detail.Node.LongName == longName && detail.Node.Position is not null && detail.Node.IsExternalPower && detail.HeardBy.Count > 0);
    }

    /// <summary>Mesh packets are handled in the background, so poll the API until the expected state shows up.</summary>
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
