using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Gateways.Events;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.AddGateway;

/// <summary>
/// Check the node is free (not a gateway yet, not someone else's node) and the limit → new login tied to that node →
/// store the gateway as Pending (hash only) → return the password once.
/// </summary>
public sealed class AddGatewayHandler(
    IMeshGatewayRepository gateways,
    INodeRegistrationRepository registrations,
    IMeshNodeRepository nodes,
    IGatewayCredentialGenerator credentials,
    IGatewaySetup setup,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<AddGatewayCommand, GatewayCredentialsDto>
{
    public async ValueTask<GatewayCredentialsDto> Handle(AddGatewayCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.StableId();
        var nodeId = MeshNode.FormatNodeId(command.NodeNum);

        // A node is one gateway at most: to add it again (e.g. lost password), remove the old one first.
        if (await gateways.GetActiveByNodeNumAsync(command.NodeNum, cancellationToken) is { } existing)
        {
            throw new DomainException(existing.IsOwnedBy(userId)
                ? $"Node {nodeId} is already one of your gateways. Remove it first to add it again."
                : $"Node {nodeId} is already a gateway of someone else.");
        }

        if (await registrations.GetActiveForNodeAsync(command.NodeNum, cancellationToken) is { Status: RegistrationStatus.Verified } registration
            && registration.UserId != userId)
        {
            throw new DomainException($"Node {nodeId} is registered to another user.");
        }

        if (await gateways.CountActiveForOwnerAsync(userId, cancellationToken) >= MeshGateway.MaxPerOwner)
        {
            throw new DomainException($"You can have at most {MeshGateway.MaxPerOwner} gateways. Remove one you no longer use first.");
        }

        var login = credentials.NewCredentials();
        var gateway = MeshGateway.IssueMqtt(
            userId, currentUser.Name, command.NodeNum, setup.Broker, login.UserName, login.Password, timeProvider.GetUtcNow());
        await gateways.AddAsync(gateway, cancellationToken);
        await gateways.SaveChangesAsync(cancellationToken);

        var node = await nodes.GetAsync(command.NodeNum, cancellationToken);
        var dto = gateway.ToDto(node, [], userId);
        await publisher.Publish(new GatewayStatusChangedNotification(gateway.ToDto(node, [], null)), cancellationToken);
        return new GatewayCredentialsDto(dto, login.UserName, login.Password, setup.Setup);
    }
}
