using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.ReceiveTextMessage;

/// <summary>Stores an inbound text (once, even if the radio repeats it) and pushes it to the chat.</summary>
public sealed class ReceiveTextMessageHandler(IMeshMessageRepository messages, IPublisher publisher)
    : ICommandHandler<ReceiveTextMessageCommand>
{
    public async ValueTask<Unit> Handle(ReceiveTextMessageCommand command, CancellationToken cancellationToken)
    {
        if (await messages.InboundExistsAsync(command.From, command.PacketId, cancellationToken))
        {
            return Unit.Value;
        }

        var message = MeshMessage.Received(
            command.From,
            command.To == MeshMessaging.BroadcastNodeNum ? null : command.To,
            command.Channel,
            command.Text,
            command.PacketId,
            command.Snr,
            command.Rssi,
            command.HopsAway,
            command.ReceivedAt);

        await messages.AddAsync(message, cancellationToken);
        await messages.SaveChangesAsync(cancellationToken);
        await publisher.Publish(new MessageReceivedNotification(message.ToDto()), cancellationToken);
        return Unit.Value;
    }
}
