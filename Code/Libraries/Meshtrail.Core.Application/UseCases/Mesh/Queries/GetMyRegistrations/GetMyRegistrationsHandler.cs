using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.VerifyRegistration;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetMyRegistrations;

public sealed class GetMyRegistrationsHandler(
    INodeRegistrationRepository registrations,
    IMeshMessageRepository messages,
    ICurrentUser currentUser) : IQueryHandler<GetMyRegistrationsQuery, IReadOnlyList<RegistrationDto>>
{
    public async ValueTask<IReadOnlyList<RegistrationDto>> Handle(GetMyRegistrationsQuery query, CancellationToken cancellationToken)
    {
        var mine = await registrations.GetActiveForUserAsync(currentUser.StableId(), cancellationToken);
        var result = new List<RegistrationDto>(mine.Count);
        foreach (var registration in mine)
        {
            result.Add(registration.ToDto(await VerifyRegistrationHandler.VerificationStatusAsync(messages, registration, cancellationToken)));
        }

        return result;
    }
}
