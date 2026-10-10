using Mediator;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.RequestPasswordReset;

/// <summary>Mails a password-reset link when the address has an account (the answer is always the same).</summary>
public sealed record RequestPasswordResetCommand(string Email) : ICommand;
