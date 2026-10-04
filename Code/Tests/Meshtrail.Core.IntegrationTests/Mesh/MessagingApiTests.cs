using System.Net;
using System.Net.Http.Json;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using static Meshtrail.Core.IntegrationTests.Mesh.MeshApiTestHelpers;
using static Meshtrail.Core.IntegrationTests.Mesh.MessagingApiTestHelpers;

namespace Meshtrail.Core.IntegrationTests.Mesh;

/// <summary>Registration (contact link + code) and messaging (queue → sent → acked/failed) end to end.</summary>
[TestClass]
public sealed class MessagingApiTests
{
    private HttpClient _client = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _client = AssemblySetup.Factory.CreateClient();

        // Sending needs an online gateway.
        await EventuallyAsync(() => TryGetAsync<GatewayStatusDto>(_client, GatewayUrl), status => status.Status == "Online");
    }

    [TestCleanup]
    public void Cleanup() => _client.Dispose();

    [TestMethod]
    public async Task Register_ValidLinkAndCode_VerifiesAndGivesKeyToGateway()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        var key = Key(7);

        // Act
        var claimed = await RegisterAsync(_client, nodeNum, key);
        var code = await SentCodeAsync(nodeNum);
        var response = await _client.PostAsJsonAsync($"{RegistrationsUrl}/{claimed.Id}/verify", new VerifyRegistrationRequest(code));

        // Assert
        claimed.Status.ShouldBe("Claimed");
        claimed.AttemptsLeft.ShouldBe(5);
        claimed.LongName.ShouldBe("Hiker");
        code.Length.ShouldBe(6);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<RegistrationDto>())!.Status.ShouldBe("Verified");

        var admin = await Radio.WaitForSentPacketAsync(packet =>
            packet.To == FakeMeshRadio.GatewayNodeNum &&
            packet.Decoded.Portnum == PortNum.AdminApp &&
            AdminMessage.Parser.ParseFrom(packet.Decoded.Payload).AddContact?.NodeNum == nodeNum);
        AdminMessage.Parser.ParseFrom(admin.Decoded.Payload).AddContact.User.PublicKey.ToByteArray().ShouldBe(key);

        var node = await _client.GetFromJsonAsync<NodeDetailDto>(NodeUrl(nodeNum));
        node!.Node.IsRegistered.ShouldBeTrue();
        var mine = await _client.GetFromJsonAsync<List<RegistrationDto>>(RegistrationsUrl);
        mine!.ShouldContain(registration => registration.NodeNum == nodeNum && registration.Status == "Verified");
    }

    [TestMethod]
    public async Task Verify_WrongCode_Returns422AndCountsTheAttempt()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        var claimed = await RegisterAsync(_client, nodeNum, Key(9));
        var code = await SentCodeAsync(nodeNum);
        var wrong = code == "000000" ? "111111" : "000000";

        // Act
        var response = await _client.PostAsJsonAsync($"{RegistrationsUrl}/{claimed.Id}/verify", new VerifyRegistrationRequest(wrong));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.ShouldBe("Wrong code. 4 attempts left.");
        var mine = await _client.GetFromJsonAsync<List<RegistrationDto>>(RegistrationsUrl);
        mine!.Single(registration => registration.Id == claimed.Id).AttemptsLeft.ShouldBe(4);
    }

    [TestMethod]
    public async Task Register_KeyDiffersFromBroadcastKey_Returns422()
    {
        // Arrange: the node itself broadcasts key A, the link carries key B.
        var nodeNum = UniqueNodeNum();
        var nodeInfo = NodeInfo(nodeNum, UniqueName());
        nodeInfo.NodeInfo.User.PublicKey = ByteString.CopyFrom(Key(1));
        Radio.Inject(nodeInfo);
        await EventuallyAsync(() => TryGetAsync<NodeDetailDto>(_client, NodeUrl(nodeNum)), detail => detail.Node.HasPublicKey);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"{RegistrationsUrl}/from-contact-url", new RegisterFromContactUrlRequest(ContactLink(nodeNum, Key(2))));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [TestMethod]
    public async Task Register_NotAContactLink_Returns400WithFieldError()
    {
        // Act
        var response = await _client.PostAsJsonAsync($"{RegistrationsUrl}/from-contact-url", new RegisterFromContactUrlRequest("https://example.org/v/#abc"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys.ShouldContain("Url");
    }

    [TestMethod]
    public async Task Register_AlreadyVerified_Returns422()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        var claimed = await RegisterAsync(_client, nodeNum, Key(3));
        await _client.PostAsJsonAsync($"{RegistrationsUrl}/{claimed.Id}/verify", new VerifyRegistrationRequest(await SentCodeAsync(nodeNum)));

        // Act
        var response = await _client.PostAsJsonAsync($"{RegistrationsUrl}/from-contact-url", new RegisterFromContactUrlRequest(ContactLink(nodeNum, Key(3))));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [TestMethod]
    public async Task Revoke_OwnRegistration_Returns204AndRemovesIt()
    {
        // Arrange
        var claimed = await RegisterAsync(_client, UniqueNodeNum(), Key(4));

        // Act
        var response = await _client.DeleteAsync($"{RegistrationsUrl}/{claimed.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var mine = await _client.GetFromJsonAsync<List<RegistrationDto>>(RegistrationsUrl);
        mine!.ShouldNotContain(registration => registration.Id == claimed.Id);
    }

    [TestMethod]
    public async Task Revoke_UnknownRegistration_Returns404()
    {
        // Act
        var response = await _client.DeleteAsync($"{RegistrationsUrl}/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GetMessages_Conversation_HidesVerificationMessages()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        await RegisterAsync(_client, nodeNum, Key(5));
        await SentCodeAsync(nodeNum);

        // Act
        var page = await _client.GetFromJsonAsync<PagedResult<MessageDto>>($"{MessagesUrl}?node={nodeNum}");

        // Assert
        page!.Items.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task SendDirectMessage_AckFromDestination_BecomesAcked()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, nodeNum, UniqueName());

        // Act
        var response = await _client.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(null, nodeNum, "Are you OK?"));
        var queued = (await response.Content.ReadFromJsonAsync<MessageDto>())!;
        var packet = await Radio.WaitForSentPacketAsync(sent => sent.To == nodeNum && sent.Decoded.Portnum == PortNum.TextMessageApp);
        await WaitForStatusAsync(_client, $"{MessagesUrl}?node={nodeNum}", queued.Id, "Sent");
        Radio.Inject(RoutingReport(nodeNum, packet.Id));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        queued.Status.ShouldBe("Queued");
        queued.CreatedBy.ShouldBe(MeshtrailApiFactory.TestUser);
        packet.WantAck.ShouldBeTrue();
        packet.Decoded.Payload.ToStringUtf8().ShouldBe("Are you OK?");
        var acked = await WaitForStatusAsync(_client, $"{MessagesUrl}?node={nodeNum}", queued.Id, "Acked");
        acked.AckedAt.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task SendChannelMessage_ImplicitAckFromGateway_BecomesAcked()
    {
        // Arrange
        var text = $"hello {UniqueName()}";

        // Act
        var queued = (await (await _client.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(0, null, text))).Content.ReadFromJsonAsync<MessageDto>())!;
        var packet = await Radio.WaitForSentPacketAsync(sent => sent.Decoded?.Payload.ToStringUtf8() == text);
        Radio.Inject(RoutingReport(FakeMeshRadio.GatewayNodeNum, packet.Id));

        // Assert
        packet.To.ShouldBe(uint.MaxValue);
        await WaitForStatusAsync(_client, $"{MessagesUrl}?channel=0", queued.Id, "Acked");
    }

    [TestMethod]
    public async Task SendDirectMessage_RoutingError_BecomesFailedWithReason()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, nodeNum, UniqueName());

        // Act
        var queued = (await (await _client.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(null, nodeNum, "ping"))).Content.ReadFromJsonAsync<MessageDto>())!;
        var packet = await Radio.WaitForSentPacketAsync(sent => sent.To == nodeNum && sent.Decoded.Portnum == PortNum.TextMessageApp);
        Radio.Inject(RoutingReport(FakeMeshRadio.GatewayNodeNum, packet.Id, Routing.Types.Error.MaxRetransmit));

        // Assert
        var failed = await WaitForStatusAsync(_client, $"{MessagesUrl}?node={nodeNum}", queued.Id, "Failed");
        failed.FailureReason.ShouldBe("MaxRetransmit");
    }

    [TestMethod]
    public async Task Send_TextOver200Bytes_Returns400()
    {
        // Arrange: 67 × "€" (3 bytes each) = 201 bytes, although only 67 characters.
        var text = new string('€', 67);

        // Act
        var response = await _client.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(0, null, text));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys.ShouldContain("Text");
    }

    [TestMethod]
    public async Task Send_DirectMessageToUnknownNode_Returns404()
    {
        // Act
        var response = await _client.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(null, UniqueNodeNum(), "hi"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GetMessages_ChannelAndNodeTogether_Returns400()
    {
        // Act
        var response = await _client.GetAsync($"{MessagesUrl}?channel=0&node=12345");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task InboundTexts_AreListedOnceInChannelAndConversation()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        var channelText = $"broadcast {UniqueName()}";
        var directText = $"direct {UniqueName()}";

        // Act: the channel message arrives twice (the mesh may repeat a packet).
        Radio.Inject(TextFrom(nodeNum, uint.MaxValue, channelText, 1001));
        Radio.Inject(TextFrom(nodeNum, uint.MaxValue, channelText, 1001));
        Radio.Inject(TextFrom(nodeNum, FakeMeshRadio.GatewayNodeNum, directText, 1002));

        // Assert
        var conversation = await EventuallyAsync(
            () => TryGetAsync<PagedResult<MessageDto>>(_client, $"{MessagesUrl}?node={nodeNum}"),
            page => page.Items.Count == 1);
        conversation.Items[0].Text.ShouldBe(directText);
        conversation.Items[0].Direction.ShouldBe("Inbound");
        conversation.Items[0].PeerNodeNum.ShouldBe(nodeNum);

        var channel = await _client.GetFromJsonAsync<PagedResult<MessageDto>>($"{MessagesUrl}?channel=0&pageSize=200");
        channel!.Items.Count(message => message.Text == channelText).ShouldBe(1);
    }
}
