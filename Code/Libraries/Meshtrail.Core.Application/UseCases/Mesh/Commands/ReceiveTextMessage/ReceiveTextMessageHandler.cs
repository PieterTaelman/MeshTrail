using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.ReceiveTextMessage;

/// <summary>
/// Stores an inbound text once (several gateways may hear it). A channel message on a team's channel belongs to that
/// team's chat. Direct messages and team messages are pushed to the people who may see them; other channel messages
/// are only stored (there is no worldwide channel to show them in).
/// </summary>
public sealed class ReceiveTextMessageHandler(IMeshMessageRepository messages, ITeamRepository teams, IPublisher publisher)
    : ICommandHandler<ReceiveTextMessageCommand>
{
    public async ValueTask<Unit> Handle(ReceiveTextMessageCommand command, CancellationToken cancellationToken)
    {
        if (await messages.InboundExistsAsync(command.From, command.PacketId, cancellationToken))
        {
            return Unit.Value;
        }

        var isBroadcast = command.To == MeshMessaging.BroadcastNodeNum;
        var team = isBroadcast && !string.IsNullOrEmpty(command.ChannelName)
            ? await teams.GetByChannelNameAsync(command.ChannelName, cancellationToken)
            : null;
        var message = MeshMessage.Received(
            command.From,
            isBroadcast ? null : command.To,
            command.Channel,
            command.ChannelName,
            command.GatewayNodeNum,
            command.Text,
            command.PacketId,
            command.Snr,
            command.Rssi,
            command.HopsAway,
            command.ReceivedAt,
            team?.Id);

        await messages.AddAsync(message, cancellationToken);
        await messages.SaveChangesAsync(cancellationToken);
        if (!isBroadcast || team is not null)
        {
            await publisher.Publish(new MessageReceivedNotification(message.ToDto()), cancellationToken);
        }

        return Unit.Value;
    }
}
