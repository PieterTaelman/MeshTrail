using Mediator;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.VerifyRegistration;

/// <summary>The user enters the code shown on the node.</summary>
public sealed record VerifyRegistrationCommand(Guid Id, string Code) : ICommand<RegistrationDto>;
