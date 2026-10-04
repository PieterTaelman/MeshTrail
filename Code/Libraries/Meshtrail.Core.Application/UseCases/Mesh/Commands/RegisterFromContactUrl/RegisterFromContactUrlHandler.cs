using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RegisterFromContactUrl;

/// <summary>
/// Parse the link → make sure the node exists → clear an old claim that may be replaced → store a Claimed
/// registration and a direct message with the code → hand the message to the gateway.
/// </summary>
public sealed class RegisterFromContactUrlHandler(
    IContactUrlParser parser,
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    INodeRegistrationRepository registrations,
    IMeshMessageRepository messages,
    IMeshGateway meshGateway,
    IVerificationCodeGenerator codes,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RegisterFromContactUrlCommand, RegistrationDto>
{
    public async ValueTask<RegistrationDto> Handle(RegisterFromContactUrlCommand command, CancellationToken cancellationToken)
    {
        // The validator already checked the link; parse again to get the values.
        if (!parser.TryParse(command.Url, out var contact, out var error))
        {
            throw new DomainException(error);
        }

        var gateway = await gateways.GetAsync(MeshGateway.PrimaryKey, cancellationToken);
        MeshGateway.EnsureCanSend(gateway);

        var now = timeProvider.GetUtcNow();
        var userId = currentUser.StableId();
        var node = await GetOrDiscoverNodeAsync(contact, now, cancellationToken);
        await ReleaseOldClaimAsync(contact.NodeNum, userId, now, cancellationToken);

        var code = codes.NewCode();
        var registration = NodeRegistration.Claim(
            contact.NodeNum, userId, currentUser.Name, contact.LongName ?? node.LongName, contact.ShortName ?? node.ShortName,
            contact.PublicKey, node.PublicKey, code, now);

        var message = MeshMessage.QueueOutbound(
            0,
            contact.NodeNum,
            $"Meshtrail verification code: {code}. Valid {NodeRegistration.CodeLifetime.TotalMinutes:0} minutes.",
            MessageKind.Verification,
            meshGateway.NewPacketId(),
            gateway!.NodeNum,
            currentUser.Name,
            now);
        registration.LinkVerificationMessage(message.Id);

        await registrations.AddAsync(registration, cancellationToken);
        await messages.AddAsync(message, cancellationToken);
        await registrations.SaveChangesAsync(cancellationToken);

        meshGateway.Enqueue(MeshMessaging.ToRequest(message));
        await publisher.Publish(new MessageStatusChangedNotification(message.ToDto()), cancellationToken);
        return registration.ToDto(message.Status);
    }

    private async Task<MeshNode> GetOrDiscoverNodeAsync(ParsedContact contact, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var node = await nodes.GetAsync(contact.NodeNum, cancellationToken);
        if (node is not null)
        {
            return node;
        }

        // Not heard yet: create it so the registration has a node. Names from the link are only a first guess;
        // the key is not copied (only the node's own broadcast is trusted for that).
        node = MeshNode.Discover(contact.NodeNum, now);
        node.ApplyUser(contact.LongName, contact.ShortName, contact.HardwareModel, contact.Role, null, now);
        await nodes.AddAsync(node, cancellationToken);
        await nodes.SaveChangesAsync(cancellationToken);
        return node;
    }

    /// <summary>One registration per node: an expired claim or our own earlier claim is replaced; anything else blocks.</summary>
    private async Task ReleaseOldClaimAsync(uint nodeNum, string userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await registrations.GetActiveForNodeAsync(nodeNum, cancellationToken);
        if (existing is null)
        {
            return;
        }

        var mine = existing.UserId == userId;
        if (existing.Status == RegistrationStatus.Verified)
        {
            throw new DomainException(mine
                ? "This node is already registered to you."
                : "This node is already registered to another user. They must remove it first.");
        }

        if (!mine && !existing.IsExpired(now))
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((existing.CodeExpiresAt!.Value - now).TotalMinutes));
            throw new DomainException($"Someone else is registering this node. Try again in {minutes} minutes.");
        }

        existing.Revoke(mine ? "Replaced by a new registration." : "Code expired.", now);
        await registrations.UpdateAsync(existing, cancellationToken);

        // Save the revoke first: the database allows only one active registration per node.
        await registrations.SaveChangesAsync(cancellationToken);
    }
}
