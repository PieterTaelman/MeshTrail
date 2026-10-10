using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.VerifyRegistration;

/// <summary>
/// Check the code. Every attempt is saved (also wrong ones) before answering. On success every TCP gateway gets the
/// node's public key (add_contact), so its direct messages to the node are encrypted end to end. (MQTT gateways
/// cannot do that for us: the packet is not theirs.)
/// </summary>
public sealed class VerifyRegistrationHandler(
    INodeRegistrationRepository registrations,
    MeshNodeStores stores,
    IMeshMessageRepository messages,
    IMeshOutbox outbox,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<VerifyRegistrationCommand, RegistrationDto>
{
    public async ValueTask<RegistrationDto> Handle(VerifyRegistrationCommand command, CancellationToken cancellationToken)
    {
        var registration = await registrations.GetAsync(command.Id, cancellationToken);

        // Someone else's registration looks the same as a missing one: no hints for guessing ids.
        if (registration is null || registration.UserId != currentUser.StableId())
        {
            throw new KeyNotFoundException($"Registration {command.Id} does not exist.");
        }

        var now = timeProvider.GetUtcNow();
        var result = registration.Verify(command.Code, now);
        await registrations.UpdateAsync(registration, cancellationToken);
        await registrations.SaveChangesAsync(cancellationToken);

        switch (result)
        {
            case VerificationResult.WrongCode:
                throw new DomainException($"Wrong code. {registration.AttemptsLeft} attempts left.");
            case VerificationResult.Locked:
                throw new DomainException("Too many wrong codes. Register the node again to get a new code.");
        }

        foreach (var gateway in await stores.Gateways.GetBoundAsync(cancellationToken))
        {
            if (gateway is { Transport: GatewayTransport.Tcp, CanSend: true } && registration.PublicKey is { } key)
            {
                outbox.Enqueue(new AddContactRequest(
                    gateway.ToRoute(), registration.NodeNum, outbox.NewPacketId(), registration.LongName, registration.ShortName, key));
            }
        }

        if (await stores.Nodes.GetAsync(registration.NodeNum, cancellationToken) is { } node)
        {
            await MeshNodeUpdates.PublishAsync(stores, publisher, node, now, cancellationToken);
        }

        return registration.ToDto(await VerificationStatusAsync(messages, registration, cancellationToken));
    }

    internal static async Task<MessageStatus?> VerificationStatusAsync(
        IMeshMessageRepository messages, NodeRegistration registration, CancellationToken cancellationToken) =>
        registration.VerificationMessageId is { } id ? (await messages.GetAsync(id, cancellationToken))?.Status : null;
}
