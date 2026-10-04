using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RequeuePendingMessages;

public sealed class RequeuePendingMessagesHandler(IMeshMessageRepository messages, IMeshGateway meshGateway)
    : ICommandHandler<RequeuePendingMessagesCommand, int>
{
    public async ValueTask<int> Handle(RequeuePendingMessagesCommand command, CancellationToken cancellationToken)
    {
        var queued = await messages.GetQueuedAsync(cancellationToken);
        foreach (var message in queued)
        {
            // The gateway ignores messages it already holds, so this is safe to repeat.
            meshGateway.Enqueue(MeshMessaging.ToRequest(message));
        }

        return queued.Count;
    }
}
