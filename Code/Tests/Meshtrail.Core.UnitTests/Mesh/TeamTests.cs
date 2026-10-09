using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.ReceiveTextMessage;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.SendMessage;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Application.UseCases.Mesh.Queries.GetMessages;
using Meshtrail.Core.Application.UseCases.Teams.Commands.CreateTeam;
using Meshtrail.Core.Application.UseCases.Teams.Commands.JoinTeam;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Domain.Teams;
using Moq;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

/// <summary>Teams: domain rules, team chat out and in, and who may read what.</summary>
[TestClass]
public sealed class TeamTests
{
    [TestMethod]
    [DataRow("LongFast")]
    [DataRow("mediumfast")]
    [DataRow("")]
    [DataRow("TwelveChars1")]
    [DataRow("a/b")]
    public void Create_PublicOrInvalidChannel_Throws(string channel)
    {
        // Act + Assert
        Should.Throw<DomainException>(() => Team.Create("Alpha", channel, "JOINCODE", UserName, UserName, Now));
    }

    [TestMethod]
    public void Create_CreatorIsOwner()
    {
        // Act
        var team = AlphaTeam();

        // Assert
        team.RoleOf(UserName).ShouldBe(TeamRole.Owner);
        team.Members.ShouldHaveSingleItem();
    }

    [TestMethod]
    public void Join_Twice_AddsOnce()
    {
        // Arrange
        var team = AlphaTeam();

        // Act
        var first = team.Join("bob", "Bob", Now);
        var second = team.Join("bob", "Bob", Now);

        // Assert
        first.ShouldBeTrue();
        second.ShouldBeFalse();
        team.Members.Count.ShouldBe(2);
        team.RoleOf("bob").ShouldBe(TeamRole.Member);
    }

    [TestMethod]
    public void Leave_OnlyOwnerWithOthers_Throws()
    {
        // Arrange
        var team = AlphaTeam();
        team.Join("bob", "Bob", Now);

        // Act + Assert
        Should.Throw<DomainException>(() => team.Leave(UserName));
    }

    [TestMethod]
    public void Leave_LastMember_EndsTheTeam()
    {
        // Act + Assert
        AlphaTeam().Leave(UserName).ShouldBeTrue();
    }

    [TestMethod]
    public void RenewJoinCode_Member_Throws()
    {
        // Arrange
        var team = AlphaTeam();
        team.Join("bob", "Bob", Now);

        // Act + Assert
        Should.Throw<DomainException>(() => team.RenewJoinCode("bob", "NEWCODE1"));
    }

    [TestMethod]
    public async Task CreateTeam_ChannelTaken_Throws()
    {
        // Arrange
        var handler = new CreateTeamHandler(TeamsWith(AlphaTeam("someone-else")).Object, GatewaysWith().Object, Codes().Object, CurrentUser().Object, FixedTime());

        // Act + Assert
        var exception = await Should.ThrowAsync<DomainException>(async () => await handler.Handle(new CreateTeamCommand("Mine", "Alpha"), CancellationToken.None));
        exception.Message.ShouldContain("Another team already uses");
    }

    [TestMethod]
    public async Task JoinTeam_LowerCaseCode_Joins()
    {
        // Arrange
        var team = AlphaTeam("someone-else");
        var teams = TeamsWith(team);
        var handler = new JoinTeamHandler(teams.Object, GatewaysWith().Object, CurrentUser().Object, FixedTime());

        // Act
        var result = await handler.Handle(new JoinTeamCommand(" joincode "), CancellationToken.None);

        // Assert
        result.MyRole.ShouldBe("Member");
        team.IsMember(UserName).ShouldBeTrue();
        teams.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendTeamMessage_GoesOutThroughEveryCarryingGatewayOnTheTeamChannel()
    {
        // Arrange
        var team = AlphaTeam();
        var outbox = Outbox(packetId: 55);
        var gateways = GatewaysWith(BoundGateway(), BoundGateway(OtherGatewayNodeNum, login: "gw-other00001"));

        // Act
        var result = await SendHandler(team, gateways, outbox).Handle(new SendMessageCommand(null, team.Id, "On the ridge"), CancellationToken.None);

        // Assert
        result.TeamId.ShouldBe(team.Id);
        result.ToNodeNum.ShouldBeNull();
        outbox.Verify(port => port.Enqueue(It.Is<TextMessageRequest>(request =>
            request.NodeNum == uint.MaxValue && request.PacketId == 55 && request.Via.Channel == "Alpha")), Times.Exactly(2));
    }

    [TestMethod]
    public async Task SendTeamMessage_NotAMember_ThrowsKeyNotFound()
    {
        // Arrange
        var team = AlphaTeam("someone-else");

        // Act + Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await SendHandler(team, GatewaysWith(BoundGateway()), Outbox()).Handle(new SendMessageCommand(null, team.Id, "hi"), CancellationToken.None));
    }

    [TestMethod]
    public async Task SendTeamMessage_NoGatewayCarriesTheChannel_Throws()
    {
        // Arrange
        var team = AlphaTeam();

        // Act + Assert
        var exception = await Should.ThrowAsync<DomainException>(async () =>
            await SendHandler(team, GatewaysWith(), Outbox()).Handle(new SendMessageCommand(null, team.Id, "hi"), CancellationToken.None));
        exception.Message.ShouldContain("No online gateway carries the channel \"Alpha\"");
    }

    [TestMethod]
    public async Task Receive_OnTeamChannel_BelongsToTheTeamAndIsPushed()
    {
        // Arrange
        var team = AlphaTeam();
        var messages = MessagesReturning(null);
        var publisher = Publisher();

        // Act
        await new ReceiveTextMessageHandler(messages.Object, TeamsWith(team).Object, publisher.Object)
            .Handle(new ReceiveTextMessageCommand(HikerNodeNum, uint.MaxValue, 0, "Alpha", GatewayNodeNum, "hi team", 9, 5, -80, 1, Now), CancellationToken.None);

        // Assert
        messages.Verify(repo => repo.AddAsync(It.Is<MeshMessage>(m => m.TeamId == team.Id), It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(pub => pub.Publish(It.Is<MessageReceivedNotification>(n => n.Message.TeamId == team.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Audience_TeamMessage_IsTheMembers()
    {
        // Arrange
        var team = AlphaTeam();
        team.Join("bob", "Bob", Now);
        var audience = new MessageAudience(MessagesReturning(null).Object, TeamsWith(team).Object, Registrations().Object);

        // Act
        var people = await audience.ForAsync(Dto(teamId: team.Id), CancellationToken.None);

        // Assert
        people.ShouldBe([UserName, "bob"], ignoreOrder: true);
    }

    [TestMethod]
    public async Task Audience_Conversation_IsTheOwnerAndTheAuthors()
    {
        // Arrange
        var messages = MessagesReturning(null);
        messages.Setup(repo => repo.GetAuthorIdsToNodeAsync(HikerNodeNum, It.IsAny<CancellationToken>())).ReturnsAsync(["alice"]);
        var registration = ClaimedRegistration(userId: "owner");
        registration.Verify("123456", Now);
        var audience = new MessageAudience(messages.Object, TeamsWith().Object, Registrations(registration).Object);

        // Act
        var people = await audience.ForConversationAsync(HikerNodeNum, CancellationToken.None);

        // Assert
        people.ShouldBe(["alice", "owner"], ignoreOrder: true);
    }

    [TestMethod]
    public async Task GetConversation_NotInvolved_ReturnsAnEmptyPage()
    {
        // Arrange
        var messages = MessagesReturning(null);
        messages.Setup(repo => repo.GetAuthorIdsToNodeAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>())).ReturnsAsync(["alice"]);
        var handler = new GetMessagesHandler(
            messages.Object, TeamsWith().Object, new MessageAudience(messages.Object, TeamsWith().Object, Registrations().Object), CurrentUser("mallory").Object);

        // Act
        var page = await handler.Handle(new GetMessagesQuery(new MessageListRequest { Node = HikerNodeNum }), CancellationToken.None);

        // Assert
        page.Items.ShouldBeEmpty();
        messages.Verify(repo => repo.GetPageAsync(It.IsAny<MessageListRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetTeamChat_NotAMember_ThrowsKeyNotFound()
    {
        // Arrange
        var team = AlphaTeam("someone-else");
        var handler = new GetMessagesHandler(
            MessagesReturning(null).Object, TeamsWith(team).Object, new MessageAudience(MessagesReturning(null).Object, TeamsWith(team).Object, Registrations().Object), CurrentUser().Object);

        // Act + Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await handler.Handle(new GetMessagesQuery(new MessageListRequest { Team = team.Id }), CancellationToken.None));
    }

    private static MessageDto Dto(Guid? teamId = null) => new(
        Guid.NewGuid(), "Inbound", "Text", 0, "Alpha", GatewayNodeNum, HikerNodeNum, null, null, null, null, "hi", "Received", null, null, null, null,
        Now, null, null, null, teamId);

    private static Mock<ITeamJoinCodeGenerator> Codes()
    {
        var codes = new Mock<ITeamJoinCodeGenerator>();
        codes.Setup(generator => generator.NewCode()).Returns("NEWCODE1");
        return codes;
    }

    private static SendMessageHandler SendHandler(Team team, Mock<IMeshGatewayRepository> gateways, Mock<IMeshOutbox> outbox) =>
        new(
            NodesReturning(null).Object,
            ReceptionsOf().Object,
            gateways.Object,
            TeamsWith(team).Object,
            MessagesReturning(null).Object,
            outbox.Object,
            CurrentUser().Object,
            FixedTime(),
            Publisher().Object);
}
