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

/// <summary>Node and map endpoints, driven by uplinks injected into <see cref="FakeGatewayTransport"/>.</summary>
[TestClass]
public sealed class MeshApiTests
{
    private HttpClient _client = null!;

    [TestInitialize]
    public void Initialize() => _client = AssemblySetup.Factory.CreateClient();

    [TestCleanup]
    public void Cleanup() => _client.Dispose();

    [TestMethod]
    public async Task GetById_InjectedNode_ReturnsDiscoveredNodeAndWhoHeardIt()
    {
        // Arrange
        var gateway = await AddOnlineGatewayAsync();
        var nodeNum = UniqueNodeNum();
        var name = UniqueName();

        // Act
        var detail = await InjectNodeAsync(_client, gateway, nodeNum, name);

        // Assert
        var node = detail.Node;
        node.NodeId.ShouldBe($"!{nodeNum:x8}");
        node.ShortName.ShouldBe("TST");
        node.HardwareModel.ShouldBe("M5StackC6L");
        node.IsOnline.ShouldBeTrue();
        node.IsExternalPower.ShouldBeTrue();
        node.IsGateway.ShouldBeFalse();
        node.Position!.Latitude.ShouldBe(50.85, 0.0001);
        node.Snr.ShouldBe(6.25);
        node.HopsAway.ShouldBe(1);
        detail.LastTraceroute.ShouldBeNull();
        var heard = detail.HeardBy.ShouldHaveSingleItem();
        heard.GatewayNodeNum.ShouldBe(gateway.NodeNum);
        heard.GatewayOnline.ShouldBeTrue();
        heard.HopsAway.ShouldBe(1);
    }

    [TestMethod]
    public async Task SamePacketFromTwoGateways_StoredOnceWithTwoReceptions()
    {
        // Arrange
        var near = await AddOnlineGatewayAsync();
        var far = await AddOnlineGatewayAsync();
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, far, nodeNum, UniqueName(), hops: 2);
        var packet = Packet(nodeNum, PortNum.PositionApp, PositionAt(50.5, 4.5), hops: 0);

        // Act
        Transport.Inject(far.NodeNum, far.Login, packet);
        Transport.Inject(near.NodeNum, near.Login, packet);

        // Assert: the gateway we would send through (fewest hops) comes first.
        var detail = await EventuallyAsync(() => TryGetAsync<NodeDetailDto>(_client, NodeUrl(nodeNum)), node => node.HeardBy.Count == 2);
        detail.HeardBy[0].GatewayNodeNum.ShouldBe(near.NodeNum);
        detail.HeardBy[0].HopsAway.ShouldBe(0);
        detail.Node.Position!.Latitude.ShouldBe(50.5, 0.0001);
    }

    [TestMethod]
    public async Task GatewayNode_IsMarkedAsGateway()
    {
        // Arrange
        var gateway = await AddOnlineGatewayAsync();

        // Act
        var detail = await _client.GetFromJsonAsync<NodeDetailDto>(NodeUrl(gateway.NodeNum));

        // Assert
        detail!.Node.IsGateway.ShouldBeTrue();
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
        var gateway = await AddOnlineGatewayAsync();
        var nodeNum = UniqueNodeNum();
        var name = UniqueName();
        await InjectNodeAsync(_client, gateway, nodeNum, name);

        // Act
        var page = await _client.GetFromJsonAsync<PagedResult<NodeDto>>($"{NodesUrl}?search={name}&online=true");

        // Assert
        page!.TotalCount.ShouldBe(1);
        page.Items.Single().NodeNum.ShouldBe(nodeNum);
    }

    [TestMethod]
    public async Task GetList_Bbox_ReturnsOnlyNodesInTheView()
    {
        // Arrange: one node in Iceland, one in Belgium.
        var gateway = await AddOnlineGatewayAsync();
        var iceland = UniqueNodeNum();
        var belgium = UniqueNodeNum();
        await InjectNodeAsync(_client, gateway, iceland, UniqueName(), latitude: 64.14, longitude: -21.94);
        await InjectNodeAsync(_client, gateway, belgium, UniqueName());

        // Act
        var page = await _client.GetFromJsonAsync<PagedResult<NodeDto>>($"{NodesUrl}?bbox=-25,63,-13,67&pageSize=500");

        // Assert
        page!.Items.ShouldContain(node => node.NodeNum == iceland);
        page.Items.ShouldNotContain(node => node.NodeNum == belgium);
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
    public async Task RequestPosition_KnownNode_Returns202AndSendsViaTheGatewayFromOurVirtualNode()
    {
        // Arrange
        var gateway = await AddOnlineGatewayAsync();
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, gateway, nodeNum, UniqueName());

        // Act
        var response = await _client.PostAsync($"{NodeUrl(nodeNum)}/position-request", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var (via, packet) = await Transport.WaitForSentAsync((_, sent) => sent.To == nodeNum);
        via.GatewayNodeNum.ShouldBe(gateway.NodeNum);
        via.MqttUserName.ShouldBe(gateway.Login);
        via.Channel.ShouldBe(FakeGatewayTransport.Channel);
        packet.From.ShouldBe(VirtualNodeNum);
        packet.Decoded.Portnum.ShouldBe(PortNum.PositionApp);
        packet.Decoded.WantResponse.ShouldBeTrue();
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
        var gateway = await AddOnlineGatewayAsync();
        var nodeNum = UniqueNodeNum();
        var relay = UniqueNodeNum();
        await InjectNodeAsync(_client, gateway, nodeNum, UniqueName());

        // Act
        var response = await _client.PostAsync($"{NodeUrl(nodeNum)}/traceroute", null);
        var pending = await response.Content.ReadFromJsonAsync<NodeTracerouteDto>();
        var (_, sent) = await Transport.WaitForSentAsync((_, packet) => packet.To == nodeNum && packet.Decoded.Portnum == PortNum.TracerouteApp);
        var route = new RouteDiscovery { Route = { relay }, SnrTowards = { 24, -128 }, RouteBack = { relay }, SnrBack = { 20, 16 } };
        Transport.Inject(gateway.NodeNum, gateway.Login, Packet(nodeNum, PortNum.TracerouteApp, route, requestId: sent.Id, to: VirtualNodeNum));

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
        var gateway = await AddOnlineGatewayAsync();
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, gateway, nodeNum, UniqueName());

        // Act
        var collection = await _client.GetFromJsonAsync<MapFeatureCollectionDto>($"{MapFeaturesUrl}?layers=nodes&bbox=4,50,5,51");

        // Assert
        var feature = collection!.Features.Single(item => item.Id == $"nodes:{nodeNum}");
        feature.Geometry.Coordinates[0].ShouldBe(4.35, 0.0001);
        feature.Geometry.Coordinates[1].ShouldBe(50.85, 0.0001);
        feature.Properties["layer"]!.ToString().ShouldBe("nodes");
        feature.Properties["source"]!.ToString().ShouldBe("mesh");
    }

    [TestMethod]
    public async Task MapFeatures_GatewaysLayer_ContainsTheGatewayWithItsStatus()
    {
        // Arrange
        var gateway = await AddOnlineGatewayAsync(latitude: 50.25, longitude: 5.40);

        // Act
        var collection = await _client.GetFromJsonAsync<MapFeatureCollectionDto>($"{MapFeaturesUrl}?layers=gateways&bbox=5,50,6,51");

        // Assert
        var feature = collection!.Features.Single(item => item.Properties["nodeNum"]!.ToString() == gateway.NodeNum.ToString(System.Globalization.CultureInfo.InvariantCulture));
        feature.Properties["layer"]!.ToString().ShouldBe("gateways");
        feature.Properties["status"]!.ToString().ShouldBe("Online");
    }

    [TestMethod]
    public async Task MapFeatures_BboxElsewhere_ExcludesNode()
    {
        // Arrange
        var gateway = await AddOnlineGatewayAsync();
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, gateway, nodeNum, UniqueName());

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
