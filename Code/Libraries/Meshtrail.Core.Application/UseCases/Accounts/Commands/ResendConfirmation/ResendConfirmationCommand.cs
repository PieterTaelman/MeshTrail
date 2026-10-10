using Mediator;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.ResendConfirmation;

/// <summary>Mails a new confirmation link (only for an unconfirmed account; the answer is always the same).</summary>
public sealed record ResendConfirmationCommand(string Email) : ICommand;
