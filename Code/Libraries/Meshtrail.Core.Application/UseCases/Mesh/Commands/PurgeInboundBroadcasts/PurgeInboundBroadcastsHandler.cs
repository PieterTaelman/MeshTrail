using Mediator;
using Meshtrail.Core.Application.Repositories;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.PurgeInboundBroadcasts;

public sealed class PurgeInboundBroadcastsHandler(IMeshMessageRepository messages) : ICommandHandler<PurgeInboundBroadcastsCommand, int>
{
    public async ValueTask<int> Handle(PurgeInboundBroadcastsCommand command, CancellationToken cancellationToken) =>
        await messages.DeleteInboundBroadcastsBeforeAsync(command.Before, cancellationToken);
}
