using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordGatewayInfo;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeHeard;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordPosition;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordTracerouteResult;
using Meshtrail.Core.Infrastructure.Mesh;
using Meshtrail.Mesh.Events;
using Meshtrail.Mesh.Outbound;
using Shouldly;
using static Meshtrail.Core.UnitTests.Mesh.MeshTestHelpers;

namespace Meshtrail.Core.UnitTests.Mesh;

/// <summary>The pieces around the gateway worker: event → command mapping, rate limiter, reconnect backoff.</summary>
[TestClass]
public sealed class MeshGatewayPlumbingTests
{
    [TestMethod]
    public void ToCommand_NodeHeard_MapsToRecordNodeHeard()
    {
        // Act
        var command = MeshEventCommands.ToCommand(new NodeHeard(Now, HikerNodeNum, 5.5, -80, 1));

        // Assert
        command.ShouldBe(new RecordNodeHeardCommand(HikerNodeNum, Now, 5.5, -80, 1));
    }

    [TestMethod]
    public void ToCommand_Position_MapsToRecordPosition()
    {
        // Arrange
        var report = new PositionReport(50.1, 4.2, 120, Now.AddMinutes(-1), 32);

        // Act
        var command = MeshEventCommands.ToCommand(new PositionReceived(Now, HikerNodeNum, report)).ShouldBeOfType<RecordPositionCommand>();

        // Assert
        command.NodeNum.ShouldBe(HikerNodeNum);
        command.Position.Latitude.ShouldBe(50.1);
        command.Position.Time.ShouldBe(Now.AddMinutes(-1));
        command.ReceivedAt.ShouldBe(Now);
    }

    [TestMethod]
    public void ToCommand_GatewayInfo_MapsToRecordGatewayInfo()
    {
        // Act
        var command = MeshEventCommands.ToCommand(new GatewayInfoReceived(Now, GatewayNodeNum, "2.7.26"));

        // Assert
        command.ShouldBe(new RecordGatewayInfoCommand(GatewayNodeNum, "2.7.26"));
    }

    [TestMethod]
    public void ToCommand_Traceroute_KeepsRequestIdAndRoutes()
    {
        // Act
        var command = MeshEventCommands.ToCommand(new TracerouteReceived(Now, HikerNodeNum, 99, [11u], [6.0], [], []))
            .ShouldBeOfType<RecordTracerouteResultCommand>();

        // Assert
        command.RequestId.ShouldBe(99u);
        command.RouteTowards.ShouldBe([11u]);
    }

    [TestMethod]
    public void ToCommand_ConfigCompleted_HasNoCommand()
    {
        // Act + Assert
        MeshEventCommands.ToCommand(new ConfigCompleted(Now)).ShouldBeNull();
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
}
