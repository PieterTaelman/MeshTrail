using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RevokeRegistration;

/// <summary>The current user withdraws their registration (claimed or verified).</summary>
public sealed record RevokeRegistrationCommand(Guid Id) : ICommand;
