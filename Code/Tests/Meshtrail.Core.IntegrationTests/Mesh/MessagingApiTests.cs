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

/// <summary>Registration (contact link + code) and direct messages (queue → sent → acked/failed) end to end.</summary>
[TestClass]
public sealed class MessagingApiTests
{
    private HttpClient _client = null!;
    private TestGateway _gateway = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _client = AssemblySetup.Factory.CreateClient();

        // Sending needs an online gateway that heard the node.
        _gateway = await AddOnlineGatewayAsync();
    }

    [TestCleanup]
    public void Cleanup() => _client.Dispose();

    [TestMethod]
    public async Task Register_ValidLinkAndCode_SendsCodeViaTheGatewayAndVerifies()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, _gateway, nodeNum, UniqueName());

        // Act
        var claimed = await RegisterAsync(_client, nodeNum, Key(7));
        var (via, packet) = await Transport.WaitForSentAsync((_, sent) => sent.To == nodeNum && sent.Decoded.Portnum == PortNum.TextMessageApp);
        var code = await SentCodeAsync(nodeNum);
        var response = await _client.PostAsJsonAsync($"{RegistrationsUrl}/{claimed.Id}/verify", new VerifyRegistrationRequest(code));

        // Assert
        claimed.Status.ShouldBe("Claimed");
        claimed.AttemptsLeft.ShouldBe(5);
        claimed.LongName.ShouldBe("Hiker");
        code.Length.ShouldBe(6);
        via.GatewayNodeNum.ShouldBe(_gateway.NodeNum);
        packet.From.ShouldBe(VirtualNodeNum);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<RegistrationDto>())!.Status.ShouldBe("Verified");
        var node = await _client.GetFromJsonAsync<NodeDetailDto>(NodeUrl(nodeNum));
        node!.Node.IsRegistered.ShouldBeTrue();
        var mine = await _client.GetFromJsonAsync<List<RegistrationDto>>(RegistrationsUrl);
        mine!.ShouldContain(registration => registration.NodeNum == nodeNum && registration.Status == "Verified");
    }

    [TestMethod]
    public async Task Register_NodeNeverHeard_Returns422()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            $"{RegistrationsUrl}/from-contact-url", new RegisterFromContactUrlRequest(ContactLink(UniqueNodeNum(), Key(8))));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail!.ShouldContain("has not been heard by any gateway yet");
    }

    [TestMethod]
    public async Task Verify_WrongCode_Returns422AndCountsTheAttempt()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, _gateway, nodeNum, UniqueName());
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
        await InjectNodeAsync(_client, _gateway, nodeNum, UniqueName(), publicKey: Key(1));

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
        await InjectNodeAsync(_client, _gateway, nodeNum, UniqueName());
        var claimed = await RegisterAsync(_client, nodeNum, Key(3));
        await _client.PostAsJsonAsync($"{RegistrationsUrl}/{claimed.Id}/verify", new VerifyRegistrationRequest(await SentCodeAsync(nodeNum)));

        // Act
        var response = await _client.PostAsJsonAsync($"{RegistrationsUrl}/from-contact-url", new RegisterFromContactUrlRequest(ContactLink(nodeNum, Key(3))));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [TestMethod]
    public async Task OwnerFilter_ListsOnlyMyRegisteredNodes()
    {
        // Arrange
        var mine = UniqueNodeNum();
        var notMine = UniqueNodeNum();
        await InjectNodeAsync(_client, _gateway, mine, UniqueName());
        await InjectNodeAsync(_client, _gateway, notMine, UniqueName());
        var claimed = await RegisterAsync(_client, mine, Key(11));
        await _client.PostAsJsonAsync($"{RegistrationsUrl}/{claimed.Id}/verify", new VerifyRegistrationRequest(await SentCodeAsync(mine)));

        // Act
        var page = await _client.GetFromJsonAsync<PagedResult<NodeDto>>($"{NodesUrl}?owner=me&pageSize=500");

        // Assert
        page!.Items.ShouldContain(node => node.NodeNum == mine);
        page.Items.ShouldNotContain(node => node.NodeNum == notMine);
    }

    [TestMethod]
    public async Task Revoke_OwnRegistration_Returns204AndRemovesIt()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, _gateway, nodeNum, UniqueName());
        var claimed = await RegisterAsync(_client, nodeNum, Key(4));

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
        await InjectNodeAsync(_client, _gateway, nodeNum, UniqueName());
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
        await InjectNodeAsync(_client, _gateway, nodeNum, UniqueName());

        // Act
        var response = await _client.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(nodeNum, null, "Are you OK?"));
        var queued = (await response.Content.ReadFromJsonAsync<MessageDto>())!;
        var (_, packet) = await Transport.WaitForSentAsync((_, sent) => sent.To == nodeNum && sent.Decoded.Portnum == PortNum.TextMessageApp);
        await WaitForStatusAsync(_client, $"{MessagesUrl}?node={nodeNum}", queued.Id, "Sent");
        Transport.Inject(_gateway.NodeNum, _gateway.Login, RoutingReport(nodeNum, packet.Id));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        queued.Status.ShouldBe("Queued");
        queued.CreatedBy.ShouldBe(MeshtrailApiFactory.TestUser);
        queued.GatewayNodeNum.ShouldBe(_gateway.NodeNum);
        packet.WantAck.ShouldBeTrue();
        packet.Decoded.Payload.ToStringUtf8().ShouldBe("Are you OK?");
        var acked = await WaitForStatusAsync(_client, $"{MessagesUrl}?node={nodeNum}", queued.Id, "Acked");
        acked.AckedAt.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task SendDirectMessage_HeardByTwoGateways_GoesViaTheNearestOne()
    {
        // Arrange
        var near = await AddOnlineGatewayAsync();
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, _gateway, nodeNum, UniqueName(), hops: 3);
        Transport.Inject(near.NodeNum, near.Login, Packet(nodeNum, PortNum.TelemetryApp, new Telemetry { DeviceMetrics = new DeviceMetrics { BatteryLevel = 80 } }, hops: 0));
        await EventuallyAsync(() => TryGetAsync<NodeDetailDto>(_client, NodeUrl(nodeNum)), detail => detail.HeardBy.Count == 2);

        // Act
        await _client.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(nodeNum, null, "via the nearest gateway"));

        // Assert
        var (via, _) = await Transport.WaitForSentAsync((_, sent) => sent.To == nodeNum && sent.Decoded.Portnum == PortNum.TextMessageApp);
        via.GatewayNodeNum.ShouldBe(near.NodeNum);
        via.MqttUserName.ShouldBe(near.Login);
    }

    [TestMethod]
    public async Task SendDirectMessage_RoutingError_BecomesFailedWithReason()
    {
        // Arrange
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, _gateway, nodeNum, UniqueName());

        // Act
        var queued = (await (await _client.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(nodeNum, null, "ping"))).Content.ReadFromJsonAsync<MessageDto>())!;
        var (_, packet) = await Transport.WaitForSentAsync((_, sent) => sent.To == nodeNum && sent.Decoded.Portnum == PortNum.TextMessageApp);
        Transport.Inject(_gateway.NodeNum, _gateway.Login, RoutingReport(_gateway.NodeNum, packet.Id, Routing.Types.Error.MaxRetransmit));

        // Assert
        var failed = await WaitForStatusAsync(_client, $"{MessagesUrl}?node={nodeNum}", queued.Id, "Failed");
        failed.FailureReason.ShouldBe("MaxRetransmit");
    }

    [TestMethod]
    public async Task Send_NodeOnlyHeardByAnOfflineGateway_Returns422()
    {
        // Arrange
        var gateway = await AddOnlineGatewayAsync();
        var nodeNum = UniqueNodeNum();
        await InjectNodeAsync(_client, gateway, nodeNum, UniqueName());
        Transport.Connected(_gateway.Login);
        using var owner = ClientAs(gateway.Owner);
        await EventuallyAsync(
            async () => (await TryGetAsync<List<GatewayDto>>(owner, $"{GatewaysUrl}?mine=true"))?.SingleOrDefault(),
            dto => dto.Status == "Offline");

        // Act
        var response = await _client.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(nodeNum, null, "anyone?"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail!.ShouldStartWith("No gateway can reach node");
    }

    [TestMethod]
    public async Task Send_TextOver200Bytes_Returns400()
    {
        // Arrange: 67 × "€" (3 bytes each) = 201 bytes, although only 67 characters.
        var text = new string('€', 67);

        // Act
        var response = await _client.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(12345, null, text));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys.ShouldContain("Text");
    }

    [TestMethod]
    public async Task Send_Broadcast_Returns400()
    {
        // Act: there is no worldwide channel.
        var response = await _client.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(uint.MaxValue, null, "hello world"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys.ShouldContain("ToNodeNum");
    }

    [TestMethod]
    public async Task Send_DirectMessageToUnknownNode_Returns404()
    {
        // Act
        var response = await _client.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(UniqueNodeNum(), null, "hi"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GetMessages_WithoutNode_Returns400()
    {
        // Act
        var response = await _client.GetAsync(MessagesUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task InboundDirectMessage_HeardByTwoGateways_IsListedOnce()
    {
        // Arrange
        var second = await AddOnlineGatewayAsync();
        var nodeNum = UniqueNodeNum();
        var other = UniqueNodeNum();
        var directText = $"direct {UniqueName()}";
        var privateText = $"private {UniqueName()}";
        var toUs = TextFrom(nodeNum, VirtualNodeNum, directText, 1002);

        // Only people who wrote to the node may read the conversation: write first.
        await InjectNodeAsync(_client, _gateway, nodeNum, UniqueName());
        await _client.PostAsJsonAsync(MessagesUrl, new SendMessageRequest(nodeNum, null, "hello"));

        // Act: both gateways uplink the reply; a direct message between two other nodes passes by too.
        Transport.Inject(_gateway.NodeNum, _gateway.Login, toUs);
        Transport.Inject(second.NodeNum, second.Login, toUs);
        Transport.Inject(_gateway.NodeNum, _gateway.Login, TextFrom(nodeNum, other, privateText, 1003));

        // Assert
        await EventuallyAsync(
            () => TryGetAsync<PagedResult<MessageDto>>(_client, $"{MessagesUrl}?node={nodeNum}"),
            page => page.Items.Any(item => item.Direction == "Inbound"));
        await Task.Delay(500);
        var conversation = (await _client.GetFromJsonAsync<PagedResult<MessageDto>>($"{MessagesUrl}?node={nodeNum}"))!;
        conversation.Items.ShouldNotContain(item => item.Text == privateText);
        var message = conversation.Items.Where(item => item.Direction == "Inbound").ShouldHaveSingleItem();
        message.Text.ShouldBe(directText);
        message.Direction.ShouldBe("Inbound");
        message.PeerNodeNum.ShouldBe(nodeNum);
        message.ChannelName.ShouldBe(FakeGatewayTransport.Channel);
    }
}
