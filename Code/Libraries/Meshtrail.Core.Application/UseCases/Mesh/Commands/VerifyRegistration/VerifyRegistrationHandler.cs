using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Common;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.VerifyRegistration;

/// <summary>
/// Check the code. Every attempt is saved (also wrong ones) before answering. On success the gateway gets the node's
/// public key (add_contact), so direct messages to it are encrypted end to end.
/// </summary>
public sealed class VerifyRegistrationHandler(
    INodeRegistrationRepository registrations,
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    IMeshMessageRepository messages,
    IMeshGateway meshGateway,
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

        meshGateway.Enqueue(new AddContactRequest(
            registration.NodeNum, meshGateway.NewPacketId(), registration.LongName, registration.ShortName, registration.PublicKey));

        if (await nodes.GetAsync(registration.NodeNum, cancellationToken) is { } node)
        {
            await MeshNodeUpdates.PublishAsync(gateways, registrations, publisher, node, now, cancellationToken);
        }

        return registration.ToDto(await VerificationStatusAsync(messages, registration, cancellationToken));
    }

    internal static async Task<MessageStatus?> VerificationStatusAsync(
        IMeshMessageRepository messages, NodeRegistration registration, CancellationToken cancellationToken) =>
        registration.VerificationMessageId is { } id ? (await messages.GetAsync(id, cancellationToken))?.Status : null;
}
