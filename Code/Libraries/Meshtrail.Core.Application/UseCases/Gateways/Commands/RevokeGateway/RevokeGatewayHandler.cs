using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.RevokeGateway;

public sealed class RevokeGatewayHandler(
    IMeshGatewayRepository gateways,
    IMeshNodeRepository nodes,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RevokeGatewayCommand>
{
    public async ValueTask<Unit> Handle(RevokeGatewayCommand command, CancellationToken cancellationToken)
    {
        var gateway = await gateways.GetAsync(command.Id, cancellationToken);

        // Someone else's gateway looks the same as a missing one: no hints for guessing ids.
        if (gateway is null || !gateway.IsOwnedBy(currentUser.StableId()) || !gateway.IsActive)
        {
            throw new KeyNotFoundException($"Gateway {command.Id} does not exist.");
        }

        gateway.Revoke(timeProvider.GetUtcNow());
        await gateways.UpdateAsync(gateway, cancellationToken);
        await gateways.SaveChangesAsync(cancellationToken);
        await GatewayViews.PublishAsync(gateway, nodes, gateways, publisher, cancellationToken);
        return Unit.Value;
    }
}
