using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayStatus;

public sealed class RecordGatewayStatusHandler(
    IMeshGatewayRepository gateways,
    IMeshNodeRepository nodes,
    IMeshMessageRepository messages,
    IMeshOutbox outbox,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RecordGatewayStatusCommand>
{
    public async ValueTask<Unit> Handle(RecordGatewayStatusCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var gateway = await gateways.GetActiveByNodeNumAsync(command.GatewayNodeNum, cancellationToken);
        var isNew = gateway is null;
        if (gateway is null)
        {
            // Never seen and not connected: nothing to record.
            if (!command.Connected)
            {
                return Unit.Value;
            }

            gateway = MeshGateway.Local(command.Transport, command.GatewayNodeNum, now);
        }

        var wasSending = !isNew && gateway.CanSend;
        if (!gateway.ChangeConnection(command.Connected, command.Error, now) && !isNew)
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
        await GatewayViews.PublishAsync(gateway, nodes, gateways, publisher, cancellationToken);
        if (!wasSending && gateway.CanSend)
        {
            await MeshMessaging.RequeueAsync(messages, outbox, gateway, cancellationToken);
        }

        return Unit.Value;
    }
}
