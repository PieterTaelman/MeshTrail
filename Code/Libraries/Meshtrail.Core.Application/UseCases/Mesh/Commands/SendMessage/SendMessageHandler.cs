using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Domain.Teams;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.SendMessage;

/// <summary>
/// Direct message: check the destination → pick the gateway → store as Queued → hand to the outbox.
/// Team message: check membership → every online gateway carrying the team's channel → one stored message, the same
/// packet handed to each of those gateways (nodes that hear it twice drop the copy).
/// </summary>
public sealed class SendMessageHandler(
    IMeshNodeRepository nodes,
    INodeReceptionRepository receptions,
    IMeshGatewayRepository gateways,
    ITeamRepository teams,
    IMeshMessageRepository messages,
    IMeshOutbox outbox,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<SendMessageCommand, MessageDto>
{
    public async ValueTask<MessageDto> Handle(SendMessageCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var (message, routes) = command.TeamId is { } teamId
            ? await QueueTeamMessageAsync(teamId, command.Text, now, cancellationToken)
            : await QueueDirectMessageAsync(command.ToNodeNum!.Value, command.Text, now, cancellationToken);

        await messages.AddAsync(message, cancellationToken);
        await messages.SaveChangesAsync(cancellationToken);

        foreach (var via in routes)
        {
            outbox.Enqueue(MeshMessaging.ToRequest(message, via));
        }

        var dto = message.ToDto();
        await publisher.Publish(new MessageStatusChangedNotification(dto), cancellationToken);
        return dto;
    }

    private async Task<(MeshMessage Message, IReadOnlyList<GatewayRoute> Routes)> QueueDirectMessageAsync(
        uint toNodeNum, string text, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _ = await nodes.GetAsync(toNodeNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Node {MeshNode.FormatNodeId(toNodeNum)} is not known.");

        var via = await GatewayRoutes.PickAsync(receptions, gateways, toNodeNum, now, cancellationToken);

        // Direct messages go on the gateway's downlink channel (index 0 over TCP).
        var message = MeshMessage.QueueOutbound(
            0, via.Channel, toNodeNum, text, MessageKind.Text, outbox.NewPacketId(), via.GatewayNodeNum, via.SenderFor(outbox),
            currentUser.StableId(), currentUser.Name, now);
        return (message, [via]);
    }

    private async Task<(MeshMessage Message, IReadOnlyList<GatewayRoute> Routes)> QueueTeamMessageAsync(
        Guid teamId, string text, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var team = await teams.GetAsync(teamId, cancellationToken);

        // Someone else's team looks the same as a missing one.
        if (team is null || !team.IsMember(currentUser.StableId()))
        {
            throw new KeyNotFoundException($"Team {teamId} does not exist.");
        }

        // TCP gateways only know channel numbers, not names, so team chat goes out over MQTT/simulator gateways.
        var carrying = (await gateways.GetCarryingChannelAsync(team.ChannelName, now - Team.ChannelFreshness, cancellationToken))
            .Where(gateway => gateway is { CanSend: true } && gateway.Transport != GatewayTransport.Tcp)
            .Select(gateway => gateway.ToRoute() with { Channel = team.ChannelName })
            .ToList();
        if (carrying.Count == 0)
        {
            throw new DomainException(
                $"No online gateway carries the channel \"{team.ChannelName}\". Add the channel to a gateway with uplink and downlink on.");
        }

        var message = MeshMessage.QueueOutbound(
            0, team.ChannelName, null, text, MessageKind.Text, outbox.NewPacketId(), null, outbox.VirtualNodeNum,
            currentUser.StableId(), currentUser.Name, now, team.Id);
        return (message, carrying);
    }
}
