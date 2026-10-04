using Mediator;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetMyRegistrations;

/// <summary>The current user's claimed and verified registrations.</summary>
public sealed record GetMyRegistrationsQuery : IQuery<IReadOnlyList<RegistrationDto>>;
