using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordGatewayInfo;

/// <summary>Stores the gateway's identity. The status row always exists by now (the worker records "Connecting" first).</summary>
public sealed class RecordGatewayInfoHandler(IMeshGatewayRepository gateways, IPublisher publisher)
    : ICommandHandler<RecordGatewayInfoCommand>
{
    public async ValueTask<Unit> Handle(RecordGatewayInfoCommand command, CancellationToken cancellationToken)
    {
        var gateway = await gateways.GetAsync(MeshGateway.PrimaryKey, cancellationToken);
        if (gateway is null || !gateway.Identify(command.NodeNum, command.FirmwareVersion))
        {
            return Unit.Value;
        }

        await gateways.UpdateAsync(gateway, cancellationToken);
        await gateways.SaveChangesAsync(cancellationToken);
        await publisher.Publish(new GatewayStatusChangedNotification(gateway.ToDto()), cancellationToken);
        return Unit.Value;
    }
}
