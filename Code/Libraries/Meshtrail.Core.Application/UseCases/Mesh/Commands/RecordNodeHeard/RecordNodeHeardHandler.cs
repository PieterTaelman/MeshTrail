using Mediator;
using Meshtrail.Core.Application.Repositories;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeHeard;


/// <summary>Discovers the node if it is new and updates its last-heard time and link quality.</summary>
public sealed class RecordNodeHeardHandler(
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    INodeRegistrationRepository registrations,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RecordNodeHeardCommand>
{
    public async ValueTask<Unit> Handle(RecordNodeHeardCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await MeshNodeUpdates.ApplyAsync(nodes, gateways, registrations, publisher, command.NodeNum, now, node =>
        {
            node.RecordHeard(command.HeardAt, command.Snr, command.Rssi, command.HopsAway, now);
            return Task.CompletedTask;
        }, cancellationToken);
        return Unit.Value;
    }
}

