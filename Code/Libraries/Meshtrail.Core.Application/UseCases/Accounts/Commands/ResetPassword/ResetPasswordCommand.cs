using Mediator;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.ResetPassword;

/// <summary>Sets a new password with the token from the reset mail.</summary>
public sealed record ResetPasswordCommand(Guid UserId, string Token, string Password) : ICommand;
