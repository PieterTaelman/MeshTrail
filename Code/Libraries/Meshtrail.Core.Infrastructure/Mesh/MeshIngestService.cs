using System.Threading.Channels;
using Mediator;
using Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayConnections;
using Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayStatus;
using Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayUplink;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Mesh.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>
/// Runs every gateway transport and processes what they receive:
/// <list type="number">
/// <item>gateway check: is this an accepted gateway (registered login, right node)? Asked about once a minute;</item>
/// <item>dedupe: a packet heard by several gateways is processed once, but every gateway's reception is kept;</item>
/// <item>throttle: "gateway G heard node N" is written at most once a minute;</item>
/// <item>the rest becomes commands, run by a few workers; one node's packets always go to the same worker (in order).</item>
/// </list>
/// It never throws: one bad packet or a database hiccup may not take the API down.
/// </summary>
public sealed partial class MeshIngestService(
    IEnumerable<IGatewayTransport> transports,
    GatewayInbox inbox,
    IServiceScopeFactory scopeFactory,
    IOptions<MeshIngestOptions> ingestOptions,
    IOptions<MeshOutboundOptions> outboundOptions,
    TimeProvider timeProvider,
    ILogger<MeshIngestService> logger) : BackgroundService
{
    private readonly MeshIngestOptions _options = ingestOptions.Value;
    private readonly uint _virtualNodeNum = outboundOptions.Value.VirtualNodeNum;
    private readonly PacketDeduplicator _packets = new(ingestOptions.Value.DedupeWindow, timeProvider);
    private readonly PacketDeduplicator _heard = new(ingestOptions.Value.HeardThrottle, timeProvider);
    private readonly Dictionary<string, GatewayCheck> _gateways = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerCount = Math.Clamp(_options.Workers, 1, 32);
        var queues = Enumerable.Range(0, workerCount)
            .Select(_ => Channel.CreateBounded<IReadOnlyList<IMessage>>(new BoundedChannelOptions(1_000) { SingleReader = true, SingleWriter = true }))
            .ToArray();
        var workers = queues.Select(queue => WorkAsync(queue.Reader, stoppingToken)).ToArray();
        var running = transports.Select(transport => transport.RunAsync(inbox, stoppingToken)).ToArray();

        try
        {
            await foreach (var input in inbox.Reader.ReadAllAsync(stoppingToken))
            {
                await RouteAsync(input, queues, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }

        foreach (var queue in queues)
        {
            queue.Writer.TryComplete();
        }

        await Task.WhenAll(workers);
        await Task.WhenAll(running);
    }

    private async Task RouteAsync(GatewayInput input, Channel<IReadOnlyList<IMessage>>[] queues, CancellationToken cancellationToken)
    {
        switch (input)
        {
            case GatewayConnectionInput { Source.NodeNum: { } nodeNum } connection:
                _gateways.Remove(KeyOf(connection.Source, nodeNum));
                await SendInScopeAsync(new RecordGatewayStatusCommand(connection.Source.Transport, nodeNum, connection.Connected, connection.Error), cancellationToken);
                break;
            case BrokerConnectionsInput connections:
                await SendInScopeAsync(new RecordGatewayConnectionsCommand(connections.Broker, connections.UserNames), cancellationToken);
                break;
            case GatewayPacketInput packet:
                if (await ToCommandsAsync(packet, cancellationToken) is { Count: > 0 } commands)
                {
                    var partitionKey = packet.Message.Packet?.From ?? packet.Source.NodeNum ?? 0;
                    await queues[partitionKey % (uint)queues.Length].Writer.WriteAsync(commands, cancellationToken);
                }

                break;
        }
    }

    /// <summary>Gateway check → dedupe → translate → filter. Runs on the single router, so no locks are needed.</summary>
    internal async Task<IReadOnlyList<IMessage>> ToCommandsAsync(GatewayPacketInput input, CancellationToken cancellationToken)
    {
        if (input.Source.NodeNum is not { } gatewayNodeNum)
        {
            // A TCP gateway that has not said who it is yet.
            return [];
        }

        var packet = input.Message.Packet;
        if (packet is not null && (packet.From == 0 || packet.From == _virtualNodeNum || packet.ViaMqtt))
        {
            return [];
        }

        if (!await IsAcceptedAsync(input, gatewayNodeNum, cancellationToken))
        {
            return [];
        }

        // Heard by another gateway already: only this gateway's reception is new.
        var isFirst = packet is null || packet.Id == 0 || _packets.TryAdd(packet.From, packet.Id);
        var context = new PacketContext(gatewayNodeNum, input.Channel, to => IsOurAddress(input.Source, gatewayNodeNum, to));
        var commands = new List<IMessage>();
        foreach (var meshEvent in new PacketTranslator(gatewayNodeNum).Translate(input.Message, input.ReceivedAt))
        {
            var wanted = meshEvent switch
            {
                NodeHeard heard => _heard.TryAdd(heard.NodeNum, gatewayNodeNum),
                _ => isFirst,
            };

            if (wanted && MeshEventCommands.ToCommand(meshEvent, context) is { } command)
            {
                commands.Add(command);
            }
        }

        return commands;
    }

    /// <summary>
    /// Asks the database (via RecordGatewayUplink) whether the gateway is accepted, at most once per GatewayRefresh,
    /// or sooner when it reports a new node number or channel. Also keeps its status and "last uplink" up to date.
    /// </summary>
    private async Task<bool> IsAcceptedAsync(GatewayPacketInput input, uint gatewayNodeNum, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var key = KeyOf(input.Source, gatewayNodeNum);
        _gateways.TryGetValue(key, out var check);

        var channel = string.IsNullOrEmpty(input.Channel) ? null : input.Channel;
        var stale = check is null
            || now - check.CheckedAt >= _options.GatewayRefresh
            || check.NodeNum != gatewayNodeNum
            || (channel is not null && !check.Channels.Contains(channel));
        if (!stale)
        {
            return check!.Accepted;
        }

        var accepted = await SendInScopeAsync(
            new RecordGatewayUplinkCommand(
                input.Source.Transport, gatewayNodeNum, input.Source.MqttUserName, input.Source.Broker, input.Root, channel, input.ReceivedAt),
            cancellationToken);
        if (accepted is null)
        {
            // The database is unreachable: keep the previous answer and try again on the next packet.
            return check?.Accepted ?? false;
        }

        check = check is not null && check.NodeNum == gatewayNodeNum ? check : new GatewayCheck(gatewayNodeNum);
        check.Accepted = accepted.Value;
        check.CheckedAt = now;
        if (channel is not null)
        {
            check.Channels.Add(channel);
        }

        _gateways[key] = check;
        if (!accepted.Value)
        {
            LogGatewayRejected(input.Source.Transport.ToString(), input.Source.MqttUserName, gatewayNodeNum);
        }

        return accepted.Value;
    }

    /// <summary>Direct messages and delivery reports for us: our virtual node, or the TCP gateway itself.</summary>
    private bool IsOurAddress(GatewaySource source, uint gatewayNodeNum, uint to) =>
        to == _virtualNodeNum || (source.Transport == GatewayTransport.Tcp && to == gatewayNodeNum);

    private static string KeyOf(GatewaySource source, uint nodeNum) =>
        source.Transport == GatewayTransport.Mqtt ? $"mqtt:{source.Broker}:{source.MqttUserName}" : $"{source.Transport}:{nodeNum}";

    private async Task WorkAsync(ChannelReader<IReadOnlyList<IMessage>> queue, CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var commands in queue.ReadAllAsync(stoppingToken))
            {
                // One scope per packet, like one HTTP request: its own DbContext, so one bad packet cannot poison the next.
                await using var scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                foreach (var command in commands)
                {
                    try
                    {
                        await sender.Send(command, stoppingToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        // Log and carry on: losing one packet is better than stopping the ingest.
                        LogCommandFailed(exception, command.GetType().Name);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task SendInScopeAsync<TCommand>(TCommand command, CancellationToken cancellationToken)
        where TCommand : ICommand
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCommandFailed(exception, typeof(TCommand).Name);
        }
    }

    /// <summary>Returns null when the command failed (logged).</summary>
    private async Task<bool?> SendInScopeAsync(RecordGatewayUplinkCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCommandFailed(exception, nameof(RecordGatewayUplinkCommand));
            return null;
        }
    }

    private sealed class GatewayCheck(uint nodeNum)
    {
        public uint NodeNum { get; } = nodeNum;

        public bool Accepted { get; set; }

        public DateTimeOffset CheckedAt { get; set; }

        public HashSet<string> Channels { get; } = new(StringComparer.Ordinal);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Handling {Command} from the mesh failed")]
    private partial void LogCommandFailed(Exception exception, string command);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring uplinks of {Transport} gateway {Login} (node {NodeNum}): not an accepted gateway")]
    private partial void LogGatewayRejected(string transport, string? login, uint nodeNum);
}
