using Mediator;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.Register;

/// <summary>Creates an account (or renews an unconfirmed one) and mails the confirmation link.</summary>
public sealed record RegisterCommand(string Email, string FirstName, string LastName, string Password) : ICommand;
