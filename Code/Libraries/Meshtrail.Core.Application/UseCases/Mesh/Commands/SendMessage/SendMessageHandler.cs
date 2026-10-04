using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.SendMessage;

/// <summary>Check destination and gateway → store as Queued → hand to the gateway → tell the clients.</summary>
public sealed class SendMessageHandler(
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    IMeshMessageRepository messages,
    IMeshGateway meshGateway,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<SendMessageCommand, MessageDto>
{
    public async ValueTask<MessageDto> Handle(SendMessageCommand command, CancellationToken cancellationToken)
    {
        if (command.ToNodeNum is { } to)
        {
            _ = await nodes.GetAsync(to, cancellationToken)
                ?? throw new KeyNotFoundException($"Node {MeshNode.FormatNodeId(to)} is not known.");
        }

        var gateway = await gateways.GetAsync(MeshGateway.PrimaryKey, cancellationToken);
        MeshGateway.EnsureCanSend(gateway);

        // Direct messages always go on the primary channel; the firmware encrypts them per node when it knows the key.
        var channel = command.ToNodeNum is null ? command.ChannelIndex ?? 0 : 0;
        var message = MeshMessage.QueueOutbound(
            channel, command.ToNodeNum, command.Text, MessageKind.Text, meshGateway.NewPacketId(), gateway!.NodeNum, currentUser.Name, timeProvider.GetUtcNow());

        await messages.AddAsync(message, cancellationToken);
        await messages.SaveChangesAsync(cancellationToken);

        meshGateway.Enqueue(MeshMessaging.ToRequest(message));
        var dto = message.ToDto();
        await publisher.Publish(new MessageStatusChangedNotification(dto), cancellationToken);
        return dto;
    }
}
