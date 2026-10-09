using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeHeard;

/// <summary>Discovers the node if it is new, updates its last-heard time and stores the reception per gateway.</summary>
public sealed class RecordNodeHeardHandler(MeshNodeStores stores, TimeProvider timeProvider, IPublisher publisher)
    : ICommandHandler<RecordNodeHeardCommand>
{
    public async ValueTask<Unit> Handle(RecordNodeHeardCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await MeshNodeUpdates.ApplyAsync(stores, publisher, command.NodeNum, now, node =>
        {
            node.RecordHeard(command.HeardAt, command.Snr, command.Rssi, command.HopsAway, now);
            return stores.Receptions.RecordAsync(
                command.NodeNum, command.GatewayNodeNum, command.HeardAt, command.Snr, command.Rssi, command.HopsAway, now, cancellationToken);
        }, cancellationToken);
        return Unit.Value;
    }
}
