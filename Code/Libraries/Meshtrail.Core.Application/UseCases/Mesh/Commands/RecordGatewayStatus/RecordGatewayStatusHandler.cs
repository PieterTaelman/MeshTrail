using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordGatewayStatus;

public sealed class RecordGatewayStatusHandler(
    IMeshGatewayRepository gateways,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RecordGatewayStatusCommand>
{
    public async ValueTask<Unit> Handle(RecordGatewayStatusCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var gateway = await gateways.GetAsync(MeshGateway.PrimaryKey, cancellationToken);
        var isNew = gateway is null;
        gateway ??= MeshGateway.Register(MeshGateway.PrimaryKey, command.Mode, now);

        var changed = gateway.ChangeStatus(command.Status, command.Mode, command.Error, now);
        if (!changed && !isNew)
        {
            return Unit.Value;
        }

        if (isNew)
        {
            await gateways.AddAsync(gateway, cancellationToken);
        }
        else
        {
            await gateways.UpdateAsync(gateway, cancellationToken);
        }

        await gateways.SaveChangesAsync(cancellationToken);
        await publisher.Publish(new GatewayStatusChangedNotification(gateway.ToDto()), cancellationToken);
        return Unit.Value;
    }
}
