using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RevokeRegistration;

public sealed class RevokeRegistrationHandler(
    INodeRegistrationRepository registrations,
    MeshNodeStores stores,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RevokeRegistrationCommand>
{
    public async ValueTask<Unit> Handle(RevokeRegistrationCommand command, CancellationToken cancellationToken)
    {
        var registration = await registrations.GetAsync(command.Id, cancellationToken);
        if (registration is null || registration.UserId != currentUser.StableId())
        {
            throw new KeyNotFoundException($"Registration {command.Id} does not exist.");
        }

        var now = timeProvider.GetUtcNow();
        registration.Revoke("Removed by the user.", now);
        await registrations.UpdateAsync(registration, cancellationToken);
        await registrations.SaveChangesAsync(cancellationToken);

        if (await stores.Nodes.GetAsync(registration.NodeNum, cancellationToken) is { } node)
        {
            await MeshNodeUpdates.PublishAsync(stores, publisher, node, now, cancellationToken);
        }

        return Unit.Value;
    }
}
