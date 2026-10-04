using Mediator;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RegisterFromContactUrl;

/// <summary>Claim a node for the current user from its contact link and send it a verification code.</summary>
public sealed record RegisterFromContactUrlCommand(string Url) : ICommand<RegistrationDto>;
