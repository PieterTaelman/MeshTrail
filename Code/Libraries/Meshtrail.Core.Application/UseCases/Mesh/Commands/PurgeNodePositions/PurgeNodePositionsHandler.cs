using Mediator;
using Meshtrail.Core.Application.Repositories;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.PurgeNodePositions;

public sealed class PurgeNodePositionsHandler(IMeshNodeRepository nodes) : ICommandHandler<PurgeNodePositionsCommand, int>
{
    public async ValueTask<int> Handle(PurgeNodePositionsCommand command, CancellationToken cancellationToken) =>
        await nodes.DeletePositionsBeforeAsync(command.Before, cancellationToken);
}
