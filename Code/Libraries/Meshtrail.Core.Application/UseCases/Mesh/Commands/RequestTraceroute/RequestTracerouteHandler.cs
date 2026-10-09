using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestTraceroute;

/// <summary>Check node → pick the gateway → store a pending traceroute → queue the packet with the same id.</summary>
public sealed class RequestTracerouteHandler(
    IMeshNodeRepository nodes,
    INodeReceptionRepository receptions,
    IMeshGatewayRepository gateways,
    INodeTracerouteRepository traceroutes,
    IMeshOutbox outbox,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<RequestTracerouteCommand, NodeTracerouteDto>
{
    public async ValueTask<NodeTracerouteDto> Handle(RequestTracerouteCommand command, CancellationToken cancellationToken)
    {
        _ = await nodes.GetAsync(command.NodeNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Node {MeshNode.FormatNodeId(command.NodeNum)} is not known.");

        var now = timeProvider.GetUtcNow();
        var via = await GatewayRoutes.PickAsync(receptions, gateways, command.NodeNum, now, cancellationToken);

        var traceroute = NodeTraceroute.Request(command.NodeNum, outbox.NewPacketId(), currentUser.Name, now);
        await traceroutes.AddAsync(traceroute, cancellationToken);
        await traceroutes.SaveChangesAsync(cancellationToken);

        // Queue only after saving, so an answer can never arrive for a traceroute we have not stored.
        outbox.Enqueue(new TracerouteRequest(via, traceroute.NodeNum, traceroute.PacketId));
        return traceroute.ToDto(now);
    }
}
