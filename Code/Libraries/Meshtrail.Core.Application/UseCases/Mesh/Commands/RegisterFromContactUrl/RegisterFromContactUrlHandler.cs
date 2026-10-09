using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Events;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RegisterFromContactUrl;

/// <summary>
/// Parse the link → the node must have been heard by a gateway → clear an old claim that may be replaced → store a
/// Claimed registration and a direct message with the code → hand the message to the best gateway.
/// </summary>
public sealed class RegisterFromContactUrlHandler(
    IContactUrlParser parser,
    IMeshNodeRepository nodes,
    INodeReceptionRepository receptions,
    IMeshGatewayRepository gateways,
    INodeRegistrationRepository registrations,
    IMeshMessageRepository messages,
    IMeshOutbox outbox,
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

        // The code travels over the mesh, so a gateway must have heard the node.
        var node = await nodes.GetAsync(contact.NodeNum, cancellationToken)
            ?? throw new DomainException(
                $"Node {MeshNode.FormatNodeId(contact.NodeNum)} has not been heard by any gateway yet. Switch it on near a gateway and try again in a few minutes.");

        var now = timeProvider.GetUtcNow();
        var via = await GatewayRoutes.PickAsync(receptions, gateways, contact.NodeNum, now, cancellationToken);

        var userId = currentUser.StableId();
        await ReleaseOldClaimAsync(contact.NodeNum, userId, now, cancellationToken);

        var code = codes.NewCode();
        var registration = NodeRegistration.Claim(
            contact.NodeNum, userId, currentUser.Name, contact.LongName ?? node.LongName, contact.ShortName ?? node.ShortName,
            contact.PublicKey, node.PublicKey, code, now);

        var message = MeshMessage.QueueOutbound(
            0,
            via.Channel,
            contact.NodeNum,
            $"Meshtrail verification code: {code}. Valid {NodeRegistration.CodeLifetime.TotalMinutes:0} minutes.",
            MessageKind.Verification,
            outbox.NewPacketId(),
            via.GatewayNodeNum,
            via.SenderFor(outbox),
            userId,
            currentUser.Name,
            now);
        registration.LinkVerificationMessage(message.Id);

        await registrations.AddAsync(registration, cancellationToken);
        await messages.AddAsync(message, cancellationToken);
        await registrations.SaveChangesAsync(cancellationToken);

        outbox.Enqueue(MeshMessaging.ToRequest(message, via));
        await publisher.Publish(new MessageStatusChangedNotification(message.ToDto()), cancellationToken);
        return registration.ToDto(message.Status);
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
