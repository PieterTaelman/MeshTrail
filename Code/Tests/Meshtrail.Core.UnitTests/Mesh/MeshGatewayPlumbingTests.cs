using Mediator;
using Meshtastic.Protobufs;
using Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayInfo;
using Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayUplink;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.ReceiveTextMessage;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeHeard;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordPosition;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordRoutingResult;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordTracerouteResult;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Mesh;
using Meshtrail.Mesh.Events;
using Meshtrail.Mesh.Outbound;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

/// <summary>The pieces around the gateways: event → command mapping, ingest (dedupe, filters), rate limiter, backoff.</summary>
[TestClass]
public sealed class MeshGatewayPlumbingTests
{
    private static readonly PacketContext Context = new(GatewayNodeNum, "LongFast", to => to == VirtualNodeNum);

    [TestMethod]
    public void ToCommand_NodeHeard_MapsToRecordNodeHeardForThatGateway()
    {
        // Act
        var command = MeshEventCommands.ToCommand(new NodeHeard(Now, HikerNodeNum, 5.5, -80, 1), Context);

        // Assert
        command.ShouldBe(new RecordNodeHeardCommand(HikerNodeNum, GatewayNodeNum, Now, 5.5, -80, 1));
    }

    [TestMethod]
    public void ToCommand_Position_MapsToRecordPosition()
    {
        // Arrange
        var report = new PositionReport(50.1, 4.2, 120, Now.AddMinutes(-1), 32);

        // Act
        var command = MeshEventCommands.ToCommand(new PositionReceived(Now, HikerNodeNum, report), Context).ShouldBeOfType<RecordPositionCommand>();

        // Assert
        command.NodeNum.ShouldBe(HikerNodeNum);
        command.Position.Latitude.ShouldBe(50.1);
        command.Position.Time.ShouldBe(Now.AddMinutes(-1));
        command.ReceivedAt.ShouldBe(Now);
    }

    [TestMethod]
    public void ToCommand_GatewayFirmware_MapsToRecordGatewayInfo()
    {
        // Act
        var command = MeshEventCommands.ToCommand(new GatewayInfoReceived(Now, null, "2.7.26"), Context);

        // Assert
        command.ShouldBe(new RecordGatewayInfoCommand(GatewayNodeNum, "2.7.26"));
    }

    [TestMethod]
    public void ToCommand_Traceroute_KeepsRequestIdAndRoutes()
    {
        // Act
        var command = MeshEventCommands.ToCommand(new TracerouteReceived(Now, HikerNodeNum, 99, [11u], [6.0], [], []), Context)
            .ShouldBeOfType<RecordTracerouteResultCommand>();

        // Assert
        command.RequestId.ShouldBe(99u);
        command.RouteTowards.ShouldBe([11u]);
    }

    [TestMethod]
    public void ToCommand_TextToUs_MapsWithGatewayAndChannel()
    {
        // Act
        var command = MeshEventCommands.ToCommand(new TextReceived(Now, HikerNodeNum, VirtualNodeNum, 0, "hi", 5, 1.0, -90, 1), Context);

        // Assert
        command.ShouldBe(new ReceiveTextMessageCommand(HikerNodeNum, VirtualNodeNum, 0, "LongFast", GatewayNodeNum, "hi", 5, 1.0, -90, 1, Now));
    }

    [TestMethod]
    public void ToCommand_DirectMessageBetweenOtherNodes_IsSkipped()
    {
        // Act + Assert: private and not ours.
        MeshEventCommands.ToCommand(new TextReceived(Now, HikerNodeNum, OtherGatewayNodeNum, 0, "secret", 5, null, null, null), Context).ShouldBeNull();
    }

    [TestMethod]
    public void ToCommand_RoutingForUs_MapsToRecordRoutingResult()
    {
        // Act
        var command = MeshEventCommands.ToCommand(new RoutingReceived(Now, HikerNodeNum, VirtualNodeNum, 5, "None"), Context);

        // Assert
        command.ShouldBe(new RecordRoutingResultCommand(HikerNodeNum, 5, "None"));
    }

    [TestMethod]
    public void ToCommand_RoutingForSomeoneElse_IsSkipped()
    {
        // Act + Assert
        MeshEventCommands.ToCommand(new RoutingReceived(Now, HikerNodeNum, OtherGatewayNodeNum, 5, "None"), Context).ShouldBeNull();
    }

    [TestMethod]
    public void ToCommand_ConfigCompleted_HasNoCommand()
    {
        // Act + Assert
        MeshEventCommands.ToCommand(new ConfigCompleted(Now), Context).ShouldBeNull();
    }

    [TestMethod]
    public void Deduplicator_SameKeyWithinWindow_IsSeenOnce()
    {
        // Arrange
        var time = FixedTime();
        var deduplicator = new PacketDeduplicator(TimeSpan.FromMinutes(10), time);

        // Act
        var first = deduplicator.TryAdd(HikerNodeNum, 1);
        time.Advance(TimeSpan.FromMinutes(9));
        var second = deduplicator.TryAdd(HikerNodeNum, 1);
        time.Advance(TimeSpan.FromMinutes(2));
        var afterWindow = deduplicator.TryAdd(HikerNodeNum, 1);

        // Assert
        first.ShouldBeTrue();
        second.ShouldBeFalse();
        afterWindow.ShouldBeTrue();
    }

    [TestMethod]
    public async Task Ingest_SamePacketFromTwoGateways_ProcessedOnceButBothReceptionsKept()
    {
        // Arrange
        var ingest = Ingest(out _);
        var position = Packet(HikerNodeNum, PortNum.PositionApp, PositionAt(50.1, 4.2));

        // Act
        var first = await ingest.ToCommandsAsync(Input(GatewayNodeNum, position), CancellationToken.None);
        var second = await ingest.ToCommandsAsync(Input(OtherGatewayNodeNum, position), CancellationToken.None);

        // Assert
        first.Select(command => command.GetType()).ShouldBe([typeof(RecordNodeHeardCommand), typeof(RecordPositionCommand)]);
        second.ShouldHaveSingleItem().ShouldBeOfType<RecordNodeHeardCommand>().GatewayNodeNum.ShouldBe(OtherGatewayNodeNum);
    }

    [TestMethod]
    public async Task Ingest_NodeHeardAgainSoon_ReceptionIsThrottled()
    {
        // Arrange
        var ingest = Ingest(out _);

        // Act
        await ingest.ToCommandsAsync(Input(GatewayNodeNum, Packet(HikerNodeNum, PortNum.PositionApp, PositionAt(50.1, 4.2), id: 1)), CancellationToken.None);
        var again = await ingest.ToCommandsAsync(Input(GatewayNodeNum, Packet(HikerNodeNum, PortNum.PositionApp, PositionAt(50.2, 4.2), id: 2)), CancellationToken.None);

        // Assert: the new position is processed, the "heard" write is not repeated.
        again.ShouldHaveSingleItem().ShouldBeOfType<RecordPositionCommand>();
    }

    [TestMethod]
    public async Task Ingest_RejectedGateway_ProducesNothing()
    {
        // Arrange
        var ingest = Ingest(out _, accepted: false);

        // Act
        var commands = await ingest.ToCommandsAsync(Input(GatewayNodeNum, Packet(HikerNodeNum, PortNum.PositionApp, PositionAt(50.1, 4.2))), CancellationToken.None);

        // Assert
        commands.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task Ingest_GatewayCheck_IsAskedOncePerRefreshInterval()
    {
        // Arrange
        var ingest = Ingest(out var sender);

        // Act
        for (uint id = 1; id <= 5; id++)
        {
            await ingest.ToCommandsAsync(Input(GatewayNodeNum, Packet(HikerNodeNum, PortNum.PositionApp, PositionAt(50.1, 4.2), id: id)), CancellationToken.None);
        }

        // Assert
        sender.Verify(s => s.Send(It.IsAny<RecordGatewayUplinkCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Ingest_PacketFromOurVirtualNode_IsIgnored()
    {
        // Arrange: another gateway hears our own downlink on the air.
        var ingest = Ingest(out _);

        // Act
        var commands = await ingest.ToCommandsAsync(Input(OtherGatewayNodeNum, Packet(VirtualNodeNum, PortNum.TextMessageApp, new Position())), CancellationToken.None);

        // Assert
        commands.ShouldBeEmpty();
    }

    [TestMethod]
    public void Downlink_SetsVirtualSenderAndHopLimit()
    {
        // Act
        var packet = MeshPackets.ForDownlink(MeshPackets.Text(HikerNodeNum, 0, "hi", 42), VirtualNodeNum, 9);

        // Assert
        packet.From.ShouldBe(VirtualNodeNum);
        packet.HopLimit.ShouldBe(MeshPackets.MaxHopLimit);
        packet.HopStart.ShouldBe(MeshPackets.MaxHopLimit);
        packet.WantAck.ShouldBeTrue();
    }

    [TestMethod]
    public void RateLimiter_FirstPacket_MayGoNow()
    {
        // Arrange
        var limiter = new OutboundRateLimiter(TimeSpan.FromSeconds(10), FixedTime());

        // Act + Assert
        limiter.GetDelay().ShouldBe(TimeSpan.Zero);
    }

    [TestMethod]
    public async Task RateLimiter_SecondPacketTooSoon_WaitsTheRest()
    {
        // Arrange
        var time = FixedTime();
        var limiter = new OutboundRateLimiter(TimeSpan.FromSeconds(10), time);
        await limiter.WaitTurnAsync(CancellationToken.None);

        // Act
        time.Advance(TimeSpan.FromSeconds(4));

        // Assert
        limiter.GetDelay().ShouldBe(TimeSpan.FromSeconds(6));
    }

    [TestMethod]
    public async Task RateLimiter_WaitTurn_CompletesOnlyAfterInterval()
    {
        // Arrange
        var time = FixedTime();
        var limiter = new OutboundRateLimiter(TimeSpan.FromSeconds(10), time);
        await limiter.WaitTurnAsync(CancellationToken.None);

        // Act
        var second = limiter.WaitTurnAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(9));
        var completedEarly = second.IsCompleted;
        time.Advance(TimeSpan.FromSeconds(1));
        await second;

        // Assert
        completedEarly.ShouldBeFalse();
        limiter.GetDelay().ShouldBe(TimeSpan.FromSeconds(10));
    }

    [TestMethod]
    public async Task RateLimiter_AfterInterval_NoWait()
    {
        // Arrange
        var time = FixedTime();
        var limiter = new OutboundRateLimiter(TimeSpan.FromSeconds(10), time);
        await limiter.WaitTurnAsync(CancellationToken.None);

        // Act
        time.Advance(TimeSpan.FromSeconds(11));

        // Assert
        limiter.GetDelay().ShouldBe(TimeSpan.Zero);
    }

    [TestMethod]
    [DataRow(0, 0.8, 1.2)]
    [DataRow(3, 6.4, 9.6)]
    public void Backoff_GrowsExponentiallyWithJitter(int attempt, double minSeconds, double maxSeconds)
    {
        // Act
        var low = ReconnectBackoff.Delay(attempt, 0.0);
        var high = ReconnectBackoff.Delay(attempt, 0.999999);

        // Assert
        low.TotalSeconds.ShouldBe(minSeconds, 0.001);
        high.TotalSeconds.ShouldBe(maxSeconds, 0.001);
    }

    [TestMethod]
    public void Backoff_ManyAttempts_NeverExceedsMaximum()
    {
        // Act
        var delay = ReconnectBackoff.Delay(1000, 0.999999);

        // Assert
        delay.ShouldBe(ReconnectBackoff.Maximum);
    }

    // ---- builders

    private static GatewayPacketInput Input(uint gatewayNodeNum, FromRadio message) =>
        new(new GatewaySource(GatewayTransport.Mqtt, gatewayNodeNum, $"gw-{gatewayNodeNum}", "local"), "LongFast", "msh/EU_868", message, Now);

    /// <summary>An ingest whose gateway check (RecordGatewayUplink) answers <paramref name="accepted"/>.</summary>
    private static MeshIngestService Ingest(out Mock<ISender> sender, bool accepted = true)
    {
        sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<RecordGatewayUplinkCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(accepted);
        var services = new ServiceCollection().AddSingleton(sender.Object).BuildServiceProvider();

        return new MeshIngestService(
            [],
            new GatewayInbox(),
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new MeshIngestOptions()),
            Options.Create(new MeshOutboundOptions { VirtualNodeNum = VirtualNodeNum }),
            FixedTime(),
            NullLogger<MeshIngestService>.Instance);
    }
}
