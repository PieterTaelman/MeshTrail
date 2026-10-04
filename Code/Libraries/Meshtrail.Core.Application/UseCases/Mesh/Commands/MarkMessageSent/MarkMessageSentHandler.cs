using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.MarkMessageSent;

public sealed class MarkMessageSentHandler(
    IMeshMessageRepository messages,
    IMeshGatewayRepository gateways,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<MarkMessageSentCommand>
{
    public async ValueTask<Unit> Handle(MarkMessageSentCommand command, CancellationToken cancellationToken)
    {
        var message = await messages.GetAsync(command.MessageId, cancellationToken);

        // Already further along (a fast delivery report may arrive before this command): nothing to do.
        if (message is not { Status: MessageStatus.Queued })
        {
            return Unit.Value;
        }

        var gateway = await gateways.GetAsync(MeshGateway.PrimaryKey, cancellationToken);
        message.MarkSent(gateway?.NodeNum, timeProvider.GetUtcNow());
        await messages.UpdateAsync(message, cancellationToken);
        await messages.SaveChangesAsync(cancellationToken);
        await publisher.Publish(new MessageStatusChangedNotification(message.ToDto()), cancellationToken);
        return Unit.Value;
    }
}
