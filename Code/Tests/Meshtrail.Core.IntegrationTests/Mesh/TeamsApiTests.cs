using System.Net;
using System.Net.Http.Json;
using Meshtastic.Protobufs;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Contracts.Teams;
using Meshtrail.Core.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using static Meshtrail.Core.IntegrationTests.Mesh.MeshApiTestHelpers;
using static Meshtrail.Core.IntegrationTests.Mesh.MessagingApiTestHelpers;

namespace Meshtrail.Core.IntegrationTests.Mesh;

/// <summary>Teams and who may read what, with several users (X-Dev-User).</summary>
[TestClass]
public sealed class TeamsApiTests
{
    private const string TeamsUrl = "/api/v1/teams";

    [TestMethod]
    public async Task Create_Returns201AndMakesYouOwner()
    {
        // Arrange
        using var alice = ClientAs($"alice-{UniqueName()}");

        // Act
        var response = await alice.PostAsJsonAsync(TeamsUrl, new CreateTeamRequest("Rescue Alpha", UniqueChannel()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var team = (await response.Content.ReadFromJsonAsync<TeamDto>())!;
        team.MyRole.ShouldBe("Owner");
        team.JoinCode.Length.ShouldBe(8);
        team.Members.ShouldHaveSingleItem();
    }

    [TestMethod]
    public async Task Create_PublicChannel_Returns422()
    {
        // Arrange
        using var alice = ClientAs($"alice-{UniqueName()}");

        // Act
        var response = await alice.PostAsJsonAsync(TeamsUrl, new CreateTeamRequest("Everyone", "LongFast"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [TestMethod]
    public async Task Join_WithCode_MakesYouAMember()
    {
        // Arrange
        using var alice = ClientAs($"alice-{UniqueName()}");
        using var bob = ClientAs($"bob-{UniqueName()}");
        var team = await CreateTeamAsync(alice);

        // Act
        var response = await bob.PostAsJsonAsync($"{TeamsUrl}/join", new JoinTeamRequest(team.JoinCode.ToLowerInvariant()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<TeamDto>())!.MyRole.ShouldBe("Member");
        (await bob.GetFromJsonAsync<List<TeamDto>>(TeamsUrl))!.ShouldContain(item => item.Id == team.Id);
    }

    [TestMethod]
    public async Task TeamChat_InboundOnTheTeamChannel_OnlyMembersCanRead()
    {
        // Arrange
        using var alice = ClientAs($"alice-{UniqueName()}");
        using var mallory = ClientAs($"mallory-{UniqueName()}");
        var team = await CreateTeamAsync(alice);
        var gateway = await AddOnlineGatewayAsync();
        var text = $"team {UniqueName()}";

        // Act: a node on the team channel says something; the gateway uplinks it on that channel.
        Transport.Inject(gateway.NodeNum, gateway.Login, TextFrom(UniqueNodeNum(), uint.MaxValue, text, 4242), team.ChannelName);

        // Assert
        var chat = await EventuallyAsync(
            () => TryGetAsync<PagedResult<MessageDto>>(alice, $"{MessagesUrl}?team={team.Id}"),
            page => page.Items.Any(item => item.Text == text));
        chat.Items.Single(item => item.Text == text).TeamId.ShouldBe(team.Id);
        (await mallory.GetAsync($"{MessagesUrl}?team={team.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task TeamChat_Send_GoesOutThroughTheGatewayCarryingTheChannel()
    {
        // Arrange
        using var alice = ClientAs($"alice-{UniqueName()}");
        var team = await CreateTeamAsync(alice);
        var gateway = await AddOnlineGatewayAsync();
        Transport.Inject(gateway.NodeNum, gateway.Login, Packet(gateway.NodeNum, PortNum.TelemetryApp, new Telemetry(), hops: 0), team.ChannelName);
        await EventuallyAsync(
            async () => (await TryGetAsync<List<TeamDto>>(alice, TeamsUrl))?.SingleOrDefault(item => item.Id == team.Id),
            dto => dto.GatewaysOnline == 1);
        var text = $"we are at the hut {UniqueName()}"[..30];

        // Act
        var response = await alice.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(null, team.Id, text));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var (via, packet) = await Transport.WaitForSentAsync((_, sent) => sent.Decoded?.Payload.ToStringUtf8() == text);
        via.GatewayNodeNum.ShouldBe(gateway.NodeNum);
        via.Channel.ShouldBe(team.ChannelName);
        packet.To.ShouldBe(uint.MaxValue);
        packet.From.ShouldBe(VirtualNodeNum);
    }

    [TestMethod]
    public async Task TeamChat_NoGatewayCarriesTheChannel_Returns422()
    {
        // Arrange
        using var alice = ClientAs($"alice-{UniqueName()}");
        var team = await CreateTeamAsync(alice);

        // Act
        var response = await alice.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(null, team.Id, "anyone?"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail!.ShouldContain("No online gateway carries the channel");
    }

    [TestMethod]
    public async Task DirectMessages_AreOnlyVisibleToThePeopleInvolved()
    {
        // Arrange
        using var alice = ClientAs($"alice-{UniqueName()}");
        using var bob = ClientAs($"bob-{UniqueName()}");
        var gateway = await AddOnlineGatewayAsync();
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(alice, gateway, nodeNum, UniqueName());

        // Act
        await alice.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(nodeNum, null, "private"));

        // Assert
        (await alice.GetFromJsonAsync<PagedResult<MessageDto>>($"{MessagesUrl}?node={nodeNum}"))!.Items.ShouldHaveSingleItem();
        (await bob.GetFromJsonAsync<PagedResult<MessageDto>>($"{MessagesUrl}?node={nodeNum}"))!.Items.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task Leave_LastMember_EndsTheTeam()
    {
        // Arrange
        using var alice = ClientAs($"alice-{UniqueName()}");
        var team = await CreateTeamAsync(alice);

        // Act
        var response = await alice.DeleteAsync($"{TeamsUrl}/{team.Id}/members/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await alice.GetFromJsonAsync<List<TeamDto>>(TeamsUrl))!.ShouldNotContain(item => item.Id == team.Id);
    }

    /// <summary>A channel name no other test uses (max 11 characters).</summary>
    private static string UniqueChannel() => $"T{Guid.NewGuid():N}"[..11];

    private static async Task<TeamDto> CreateTeamAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(TeamsUrl, new CreateTeamRequest("Team", UniqueChannel()));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TeamDto>())!;
    }
}
