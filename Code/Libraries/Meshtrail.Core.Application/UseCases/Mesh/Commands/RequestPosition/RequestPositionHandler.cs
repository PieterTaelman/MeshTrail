using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestPosition;

/// <summary>Check the node exists → pick the gateway that can reach it → queue the request there.</summary>
public sealed class RequestPositionHandler(
    IMeshNodeRepository nodes,
    INodeReceptionRepository receptions,
    IMeshGatewayRepository gateways,
    IMeshOutbox outbox,
    TimeProvider timeProvider) : ICommandHandler<RequestPositionCommand>
{
    public async ValueTask<Unit> Handle(RequestPositionCommand command, CancellationToken cancellationToken)
    {
        _ = await nodes.GetAsync(command.NodeNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Node {MeshNode.FormatNodeId(command.NodeNum)} is not known.");

        var via = await GatewayRoutes.PickAsync(receptions, gateways, command.NodeNum, timeProvider.GetUtcNow(), cancellationToken);
        outbox.Enqueue(new PositionRequest(via, command.NodeNum, outbox.NewPacketId()));
        return Unit.Value;
    }
}
