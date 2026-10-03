using System.Net;
using System.Net.Http.Json;
using Meshtastic.Protobufs;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Map;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using static Meshtrail.Core.IntegrationTests.Mesh.MeshApiTestHelpers;

namespace Meshtrail.Core.IntegrationTests.Mesh;

/// <summary>Gateway, node and map endpoints, driven by packets injected into <see cref="FakeMeshRadio"/>.</summary>
[TestClass]
public sealed class MeshApiTests
{
    private HttpClient _client = null!;

    [TestInitialize]
    public void Initialize() => _client = AssemblySetup.Factory.CreateClient();

    [TestCleanup]
    public void Cleanup() => _client.Dispose();

    [TestMethod]
    public async Task GetGateway_AfterConnect_ReturnsOnlineWithGatewayIdentity()
    {
        // Act
        var gateway = await EventuallyAsync(
            () => TryGetAsync<GatewayStatusDto>(_client, GatewayUrl),
            status => status is { Status: "Online", FirmwareVersion: not null });

        // Assert
        gateway.NodeNum.ShouldBe(FakeMeshRadio.GatewayNodeNum);
        gateway.NodeId.ShouldBe("!1a2b3c4d");
        gateway.FirmwareVersion.ShouldBe("2.7.26.test");
        gateway.LastConnectedAt.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Reconnect_Returns202AndConnectsAgain()
    {
        // Arrange
        await EventuallyAsync(() => TryGetAsync<GatewayStatusDto>(_client, GatewayUrl), status => status.Status == "Online");
        var connectsBefore = Radio.ConnectCount;

        // Act
        var response = await _client.PostAsync($"{GatewayUrl}/reconnect", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await EventuallyAsync(() => Task.FromResult<int?>(Radio.ConnectCount), count => count > connectsBefore);
        await EventuallyAsync(() => TryGetAsync<GatewayStatusDto>(_client, GatewayUrl), status => status.Status == "Online");
    }

    [TestMethod]
    public async Task GetById_InjectedNodeInfo_ReturnsDiscoveredNode()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        var name = UniqueName();

        // Act
        var detail = await InjectNodeAsync(_client, nodeNum, name);

        // Assert
        var node = detail.Node;
        node.NodeId.ShouldBe($"!{nodeNum:x8}");
        node.ShortName.ShouldBe("TST");
        node.HardwareModel.ShouldBe("M5StackC6L");
        node.IsOnline.ShouldBeTrue();
        node.IsExternalPower.ShouldBeTrue();
        node.Position.ShouldNotBeNull();
        node.Position.Latitude.ShouldBe(50.85, 0.0001);
        detail.LastTraceroute.ShouldBeNull();
    }

    [TestMethod]
    public async Task GetById_UnknownNode_Returns404()
    {
        // Act
        var response = await _client.GetAsync(NodeUrl(UniqueNodeNum()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GetList_SearchByName_ReturnsOnlyThatNode()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        var name = UniqueName();
        await InjectNodeAsync(_client, nodeNum, name);

        // Act
        var page = await _client.GetFromJsonAsync<PagedResult<NodeDto>>($"{NodesUrl}?search={name}&online=true");

        // Assert
        page.ShouldNotBeNull();
        page.TotalCount.ShouldBe(1);
        page.Items.Single().NodeNum.ShouldBe(nodeNum);
    }

    [TestMethod]
    public async Task GetList_PageSizeTooLarge_Returns400WithFieldError()
    {
        // Act
        var response = await _client.GetAsync($"{NodesUrl}?pageSize=100000");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Keys.ShouldContain("Request.PageSize");
    }

    [TestMethod]
    public async Task PositionPacket_UpdatesNodePositionAndSignal()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, nodeNum, UniqueName());

        // Act
        Radio.Inject(Packet(nodeNum, PortNum.PositionApp, PositionAt(49.79, 5.07)));

        // Assert
        var detail = await EventuallyAsync(
            () => TryGetAsync<NodeDetailDto>(_client, NodeUrl(nodeNum)),
            node => node.Node.Position?.Latitude is > 49.7 and < 49.8);
        detail.Node.Snr.ShouldBe(6.25);
        detail.Node.Rssi.ShouldBe(-70);
        detail.Node.HopsAway.ShouldBe(1);
    }

    [TestMethod]
    public async Task RequestPosition_KnownNode_Returns202AndSendsPositionRequest()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, nodeNum, UniqueName());

        // Act
        var response = await _client.PostAsync($"{NodeUrl(nodeNum)}/position-request", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var packet = await Radio.WaitForSentPacketAsync(sent => sent.To == nodeNum);
        packet.Decoded.Portnum.ShouldBe(PortNum.PositionApp);
        packet.Decoded.WantResponse.ShouldBeTrue();
        packet.Id.ShouldNotBe(0u);
    }

    [TestMethod]
    public async Task RequestPosition_UnknownNode_Returns404()
    {
        // Act
        var response = await _client.PostAsync($"{NodeUrl(UniqueNodeNum())}/position-request", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task RequestPosition_BroadcastAddress_Returns400()
    {
        // Act
        var response = await _client.PostAsync($"{NodeUrl(uint.MaxValue)}/position-request", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Keys.ShouldContain("NodeNum");
    }

    [TestMethod]
    public async Task Traceroute_AnswerArrives_IsStoredWithRoute()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        var relay = UniqueNodeNum();
        await InjectNodeAsync(_client, nodeNum, UniqueName());

        // Act
        var response = await _client.PostAsync($"{NodeUrl(nodeNum)}/traceroute", null);
        var pending = await response.Content.ReadFromJsonAsync<NodeTracerouteDto>();
        var sent = await Radio.WaitForSentPacketAsync(packet => packet.To == nodeNum && packet.Decoded.Portnum == PortNum.TracerouteApp);
        var route = new RouteDiscovery { Route = { relay }, SnrTowards = { 24, -128 }, RouteBack = { relay }, SnrBack = { 20, 16 } };
        Radio.Inject(Packet(nodeNum, PortNum.TracerouteApp, route, requestId: sent.Id));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        pending!.Status.ShouldBe("Pending");
        pending.RequestedBy.ShouldBe(MeshtrailApiFactory.TestUser);

        var detail = await EventuallyAsync(
            () => TryGetAsync<NodeDetailDto>(_client, NodeUrl(nodeNum)),
            node => node.LastTraceroute?.Status == "Completed");
        var towards = detail.LastTraceroute!.RouteTowards.ShouldHaveSingleItem();
        towards.NodeNum.ShouldBe(relay);
        towards.Snr.ShouldBe(6.0);
        detail.LastTraceroute.RouteBack.Single().Snr.ShouldBe(5.0);
    }

    [TestMethod]
    public async Task MapFeatures_NodesLayer_ContainsNodeAsGeoJsonPoint()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, nodeNum, UniqueName());

        // Act
        var collection = await _client.GetFromJsonAsync<MapFeatureCollectionDto>($"{MapFeaturesUrl}?layers=nodes&bbox=4,50,5,51");

        // Assert
        collection.ShouldNotBeNull();
        var feature = collection.Features.Single(item => item.Id == $"nodes:{nodeNum}");
        feature.Geometry.Coordinates[0].ShouldBe(4.35, 0.0001);
        feature.Geometry.Coordinates[1].ShouldBe(50.85, 0.0001);
        feature.Properties["layer"]!.ToString().ShouldBe("nodes");
        feature.Properties["source"]!.ToString().ShouldBe("mesh");
    }

    [TestMethod]
    public async Task MapFeatures_BboxElsewhere_ExcludesNode()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, nodeNum, UniqueName());

        // Act
        var collection = await _client.GetFromJsonAsync<MapFeatureCollectionDto>($"{MapFeaturesUrl}?bbox=-10,30,-9,31");

        // Assert
        collection!.Features.ShouldNotContain(item => item.Id == $"nodes:{nodeNum}");
    }

    [TestMethod]
    [DataRow("?layers=nodes,unicorns", "Request.Layers")]
    [DataRow("?bbox=1,2,3", "Request.Bbox")]
    [DataRow("?bbox=0,60,10,50", "Request.Bbox")]
    public async Task MapFeatures_InvalidQuery_Returns400(string query, string field)
    {
        // Act
        var response = await _client.GetAsync($"{MapFeaturesUrl}{query}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Keys.ShouldContain(field);
    }
}
