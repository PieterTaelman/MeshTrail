using Mediator;
using Meshtrail.Core.Application.Repositories;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayInfo;

public sealed class RecordGatewayInfoHandler(IMeshGatewayRepository gateways, IMeshNodeRepository nodes, IPublisher publisher)
    : ICommandHandler<RecordGatewayInfoCommand>
{
    public async ValueTask<Unit> Handle(RecordGatewayInfoCommand command, CancellationToken cancellationToken)
    {
        var gateway = await gateways.GetActiveByNodeNumAsync(command.GatewayNodeNum, cancellationToken);
        if (gateway is null || !gateway.Identify(command.FirmwareVersion))
        {
            return Unit.Value;
        }

        await gateways.UpdateAsync(gateway, cancellationToken);
        await gateways.SaveChangesAsync(cancellationToken);
        await GatewayViews.PublishAsync(gateway, nodes, gateways, publisher, cancellationToken);
        return Unit.Value;
    }
}
