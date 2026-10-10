using System.Net;
using System.Net.Http.Json;
using Meshtastic.Protobufs;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using static Meshtrail.Core.IntegrationTests.Mesh.MeshApiTestHelpers;

namespace Meshtrail.Core.IntegrationTests.Mesh;

/// <summary>Adding, binding, listing and revoking gateways, and the broker's login check.</summary>
[TestClass]
public sealed class GatewaysApiTests
{
    private const string AuthUrl = "/api/v1/mqtt/auth";

    [TestMethod]
    public async Task Add_Returns201WithPendingGatewayAndCredentials()
    {
        // Arrange
        using var client = ClientAs($"owner-{UniqueName()}");

        var nodeNum = UniqueNodeNum();

        // Act
        var response = await client.PostAsJsonAsync(GatewaysUrl, new AddGatewayRequest(nodeNum));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var credentials = (await response.Content.ReadFromJsonAsync<GatewayCredentialsDto>())!;
        credentials.Gateway.NodeNum.ShouldBe(nodeNum);
        credentials.UserName.ShouldStartWith("gw-");
        credentials.Password.Length.ShouldBe(24);
        credentials.Gateway.Status.ShouldBe("Pending");
        credentials.Gateway.IsMine.ShouldBeTrue();
        credentials.Setup.Root.ShouldBe(FakeGatewayTransport.Root);
    }

    [TestMethod]
    public async Task FirstUplink_TiesTheGatewayToItsNodeAndBringsItOnline()
    {
        // Act
        var gateway = await AddOnlineGatewayAsync();

        // Assert
        using var owner = ClientAs(gateway.Owner);
        var mine = (await owner.GetFromJsonAsync<List<GatewayDto>>($"{GatewaysUrl}?mine=true"))!.ShouldHaveSingleItem();
        mine.NodeNum.ShouldBe(gateway.NodeNum);
        mine.Transport.ShouldBe("Mqtt");
        mine.Channels.ShouldBe([FakeGatewayTransport.Channel]);
        mine.MqttUserName.ShouldBe(gateway.Login);
        mine.Name.ShouldStartWith("GW ");
    }

    [TestMethod]
    public async Task Add_SameNodeTwice_Returns422UntilTheFirstIsRemoved()
    {
        // Arrange
        using var owner = ClientAs($"owner-{UniqueName()}");
        var nodeNum = UniqueNodeNum();
        var first = (await (await owner.PostAsJsonAsync(GatewaysUrl, new AddGatewayRequest(nodeNum))).Content.ReadFromJsonAsync<GatewayCredentialsDto>())!;

        // Act
        var again = await owner.PostAsJsonAsync(GatewaysUrl, new AddGatewayRequest(nodeNum));
        await owner.DeleteAsync($"{GatewaysUrl}/{first.Gateway.Id}");
        var afterRemove = await owner.PostAsJsonAsync(GatewaysUrl, new AddGatewayRequest(nodeNum));

        // Assert
        again.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await again.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail!.ShouldContain("Remove it first");
        afterRemove.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [TestMethod]
    public async Task FirstUplink_RegistersTheGatewayNodeToItsOwner()
    {
        // Act
        var gateway = await AddOnlineGatewayAsync();

        // Assert
        using var owner = ClientAs(gateway.Owner);
        var mine = await owner.GetFromJsonAsync<List<RegistrationDto>>("/api/v1/registrations");
        mine!.ShouldContain(registration => registration.NodeNum == gateway.NodeNum && registration.Status == "Verified");
    }

    [TestMethod]
    public async Task Anonymous_CanSeeTheMapButNotAddAGateway()
    {
        // Arrange
        using var anonymous = AssemblySetup.Factory.CreateClient();
        anonymous.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-valid-token");

        // Act
        var summary = await AssemblySetup.Factory.CreateClient().GetAsync($"{GatewaysUrl}/summary");
        var add = await anonymous.PostAsJsonAsync(GatewaysUrl, new AddGatewayRequest(UniqueNodeNum()));

        // Assert
        summary.StatusCode.ShouldBe(HttpStatusCode.OK);
        add.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Summary_CountsOnlineGateways()
    {
        // Arrange
        using var client = AssemblySetup.Factory.CreateClient();
        var before = (await client.GetFromJsonAsync<GatewaySummaryDto>($"{GatewaysUrl}/summary"))!;

        // Act
        await AddOnlineGatewayAsync();

        // Assert
        var after = (await client.GetFromJsonAsync<GatewaySummaryDto>($"{GatewaysUrl}/summary"))!;
        after.Online.ShouldBe(before.Online + 1);
        after.Total.ShouldBe(before.Total + 1);
    }

    [TestMethod]
    public async Task LoginUsedFromAnotherNode_UplinksAreIgnored()
    {
        // Arrange
        var gateway = await AddOnlineGatewayAsync();
        var stranger = UniqueNodeNum();
        using var client = AssemblySetup.Factory.CreateClient();

        // Act: the same login now claims to be another node.
        Transport.Inject(UniqueNodeNum(), gateway.Login, Packet(stranger, PortNum.PositionApp, PositionAt(50.1, 4.1)));
        await Task.Delay(1000);

        // Assert
        (await client.GetAsync(NodeUrl(stranger))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Revoke_Mine_Returns204AndItsUplinksAreIgnoredAfterwards()
    {
        // Arrange
        var gateway = await AddOnlineGatewayAsync();
        using var owner = ClientAs(gateway.Owner);
        var id = (await owner.GetFromJsonAsync<List<GatewayDto>>($"{GatewaysUrl}?mine=true"))!.Single().Id;

        // Act
        var response = await owner.DeleteAsync($"{GatewaysUrl}/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await owner.GetFromJsonAsync<List<GatewayDto>>($"{GatewaysUrl}?mine=true"))!.ShouldBeEmpty();
        var login = await AuthenticateAsync(gateway.Login, "anything", MeshtrailApiFactory.ServiceKey);
        login.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Revoke_SomeoneElsesGateway_Returns404()
    {
        // Arrange
        var gateway = await AddOnlineGatewayAsync();
        using var owner = ClientAs(gateway.Owner);
        var id = (await owner.GetFromJsonAsync<List<GatewayDto>>($"{GatewaysUrl}?mine=true"))!.Single().Id;
        using var other = ClientAs($"other-{UniqueName()}");

        // Act
        var response = await other.DeleteAsync($"{GatewaysUrl}/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GetList_All_ContainsOnlineGatewaysWithoutPrivateFields()
    {
        // Arrange
        var gateway = await AddOnlineGatewayAsync();
        using var client = AssemblySetup.Factory.CreateClient();

        // Act
        var all = await client.GetFromJsonAsync<List<GatewayDto>>(GatewaysUrl);

        // Assert
        var listed = all!.Single(dto => dto.NodeNum == gateway.NodeNum);
        listed.IsMine.ShouldBeFalse();
        listed.MqttUserName.ShouldBeNull();
        listed.Status.ShouldBe("Online");
    }

    [TestMethod]
    public async Task BrokerConnections_LoginMissing_GatewayGoesOffline()
    {
        // Arrange
        var gateway = await AddOnlineGatewayAsync();
        using var owner = ClientAs(gateway.Owner);

        // Act
        Transport.Connected();

        // Assert
        var mine = await EventuallyAsync(
            async () => (await TryGetAsync<List<GatewayDto>>(owner, $"{GatewaysUrl}?mine=true"))?.SingleOrDefault(),
            dto => dto.Status == "Offline");
        mine.LastError.ShouldBe("Not connected to the broker.");
    }

    [TestMethod]
    public async Task Auth_RightPassword_IsAllowed()
    {
        // Arrange
        using var client = ClientAs($"owner-{UniqueName()}");
        var credentials = (await (await client.PostAsJsonAsync(GatewaysUrl, new AddGatewayRequest(UniqueNodeNum()))).Content.ReadFromJsonAsync<GatewayCredentialsDto>())!;

        // Act
        var allowed = await AuthenticateAsync(credentials.UserName, credentials.Password, MeshtrailApiFactory.ServiceKey);
        var wrong = await AuthenticateAsync(credentials.UserName, "wrong", MeshtrailApiFactory.ServiceKey);

        // Assert
        allowed.ShouldBeTrue();
        wrong.ShouldBeFalse();
    }

    [TestMethod]
    public async Task Auth_WithoutServiceKey_Returns401()
    {
        // Arrange
        using var client = AssemblySetup.Factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(AuthUrl, new AuthenticateGatewayRequest("c", "gw-x", "y"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<bool> AuthenticateAsync(string userName, string password, string serviceKey)
    {
        using var client = AssemblySetup.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, AuthUrl) { Content = JsonContent.Create(new AuthenticateGatewayRequest("client", userName, password)) };
        request.Headers.Add("X-Meshtrail-Service-Key", serviceKey);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthenticateGatewayResponse>())!.Allowed;
    }
}
