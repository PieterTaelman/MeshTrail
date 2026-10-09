using System.Collections.Concurrent;
using Meshtastic.Protobufs;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Mesh;

namespace Meshtrail.Core.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for the MQTT broker in integration tests: a test injects uplinks as if any gateway login sent them,
/// tells which logins are connected, and sees every downlink the API sends (with the gateway it was meant for).
/// </summary>
internal sealed class FakeGatewayTransport : IGatewayTransport
{
    public const string BrokerName = "local";
    public const string Channel = "LongFast";
    public const string Root = "msh/EU_868";

    private readonly ConcurrentQueue<(GatewayRoute Via, MeshPacket Packet)> _sent = new();
    private readonly TaskCompletionSource<GatewayInbox> _inbox = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public GatewayTransport Kind => GatewayTransport.Mqtt;

    public string? Broker => BrokerName;

    public string Description => "fake broker";

    public bool IsConnected => true;

    public string? LastError => null;

    public async Task RunAsync(GatewayInbox inbox, CancellationToken stoppingToken)
    {
        _inbox.TrySetResult(inbox);
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // The test host stops.
        }
    }

    public Task SendAsync(GatewayRoute via, MeshPacket packet, CancellationToken cancellationToken)
    {
        _sent.Enqueue((via, packet));
        return Task.CompletedTask;
    }

    /// <summary>Pretends gateway <paramref name="gatewayNodeNum"/> (logged in as <paramref name="login"/>) uplinked this packet.</summary>
    public void Inject(uint gatewayNodeNum, string login, MeshPacket packet, string channel = Channel) =>
        Inbox().Post(new GatewayPacketInput(
            new GatewaySource(GatewayTransport.Mqtt, gatewayNodeNum, login, BrokerName), channel, Root, new FromRadio { Packet = packet }, DateTimeOffset.UtcNow));

    /// <summary>Pretends the broker reported these logins as connected (all others are not).</summary>
    public void Connected(params string[] logins) => Inbox().Post(new BrokerConnectionsInput(BrokerName, logins, DateTimeOffset.UtcNow));

    /// <summary>Waits until the API sent a packet that matches, or fails after <paramref name="timeout"/>.</summary>
    public async Task<(GatewayRoute Via, MeshPacket Packet)> WaitForSentAsync(Func<GatewayRoute, MeshPacket, bool> match, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < deadline)
        {
            foreach (var sent in _sent)
            {
                if (match(sent.Via, sent.Packet))
                {
                    return sent;
                }
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("The API did not send the expected packet in time.");
    }

    public bool WasSent(Func<GatewayRoute, MeshPacket, bool> match) => _sent.Any(sent => match(sent.Via, sent.Packet));

    private GatewayInbox Inbox() =>
        _inbox.Task.Wait(TimeSpan.FromSeconds(10)) ? _inbox.Task.Result : throw new TimeoutException("The API did not start the gateway transports.");
}
