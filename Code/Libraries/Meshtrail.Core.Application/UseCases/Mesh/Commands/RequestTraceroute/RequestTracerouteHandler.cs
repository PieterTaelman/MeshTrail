using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestTraceroute;

/// <summary>Check node and gateway → store a pending traceroute → queue the packet with the same id.</summary>
public sealed class RequestTracerouteHandler(
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    INodeTracerouteRepository traceroutes,
    IMeshGateway meshGateway,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<RequestTracerouteCommand, NodeTracerouteDto>
{
    public async ValueTask<NodeTracerouteDto> Handle(RequestTracerouteCommand command, CancellationToken cancellationToken)
    {
        _ = await nodes.GetAsync(command.NodeNum, cancellationToken)
            ?? throw new KeyNotFoundException($"Node {MeshNode.FormatNodeId(command.NodeNum)} is not known.");

        var gateway = await gateways.GetAsync(MeshGateway.PrimaryKey, cancellationToken);
        MeshGateway.EnsureCanSend(gateway);

        var now = timeProvider.GetUtcNow();
        var traceroute = NodeTraceroute.Request(command.NodeNum, meshGateway.NewPacketId(), currentUser.Name, now);
        await traceroutes.AddAsync(traceroute, cancellationToken);
        await traceroutes.SaveChangesAsync(cancellationToken);

        // Queue only after saving, so an answer can never arrive for a traceroute we have not stored.
        meshGateway.Enqueue(new TracerouteRequest(traceroute.NodeNum, traceroute.PacketId));
        return traceroute.ToDto(now);
    }
}
