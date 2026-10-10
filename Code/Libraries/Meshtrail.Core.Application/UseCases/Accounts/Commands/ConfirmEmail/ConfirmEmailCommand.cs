using Mediator;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.ConfirmEmail;

/// <summary>The user clicked the link in the confirmation mail.</summary>
public sealed record ConfirmEmailCommand(Guid UserId, string Token) : ICommand;
