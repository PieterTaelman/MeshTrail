using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Gateways.Events;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.AddGateway;

/// <summary>Check the limit → new login → store the gateway as Pending (hash only) → return the password once.</summary>
public sealed class AddGatewayHandler(
    IMeshGatewayRepository gateways,
    IGatewayCredentialGenerator credentials,
    IGatewaySetup setup,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<AddGatewayCommand, GatewayCredentialsDto>
{
    public async ValueTask<GatewayCredentialsDto> Handle(AddGatewayCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.StableId();
        if (await gateways.CountActiveForOwnerAsync(userId, cancellationToken) >= MeshGateway.MaxPerOwner)
        {
            throw new DomainException($"You can have at most {MeshGateway.MaxPerOwner} gateways. Remove one you no longer use first.");
        }

        var login = credentials.NewCredentials();
        var gateway = MeshGateway.IssueMqtt(userId, currentUser.Name, setup.Broker, login.UserName, login.Password, timeProvider.GetUtcNow());
        await gateways.AddAsync(gateway, cancellationToken);
        await gateways.SaveChangesAsync(cancellationToken);

        var dto = gateway.ToDto(null, [], userId);
        await publisher.Publish(new GatewayStatusChangedNotification(gateway.ToDto(null, [], null)), cancellationToken);
        return new GatewayCredentialsDto(dto, login.UserName, login.Password, setup.Setup);
    }
}
