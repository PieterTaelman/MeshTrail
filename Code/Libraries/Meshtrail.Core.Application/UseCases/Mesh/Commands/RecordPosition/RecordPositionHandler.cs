using Mediator;
using Meshtrail.Core.Application.Repositories;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordPosition;


/// <summary>Updates the node's last position and appends it to the position history.</summary>
public sealed class RecordPositionHandler(
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RecordPositionCommand>
{
    public async ValueTask<Unit> Handle(RecordPositionCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await MeshNodeUpdates.ApplyAsync(nodes, gateways, publisher, command.NodeNum, now, node =>
            MeshNodeUpdates.RecordPositionAsync(nodes, node, command.Position, command.ReceivedAt, now, cancellationToken),
            cancellationToken);
        return Unit.Value;
    }
}

