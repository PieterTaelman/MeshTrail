using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestPosition;

/// <summary>Check the node exists and the gateway can send → queue the request.</summary>
public sealed class RequestPositionHandler(
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    IMeshGateway meshGateway) : ICommandHandler<RequestPositionCommand>
{
    public async ValueTask<Unit> Handle(RequestPositionCommand command, CancellationToken cancellationToken)
    {
        _ = await nodes.GetAsync(command.NodeNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Node {MeshNode.FormatNodeId(command.NodeNum)} is not known.");

        var gateway = await gateways.GetAsync(MeshGateway.PrimaryKey, cancellationToken);
        MeshGateway.EnsureCanSend(gateway);

        meshGateway.Enqueue(new PositionRequest(command.NodeNum, meshGateway.NewPacketId()));
        return Unit.Value;
    }
}
