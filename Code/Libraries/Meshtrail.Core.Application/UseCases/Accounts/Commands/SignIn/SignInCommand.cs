using Mediator;
using Meshtrail.Core.Contracts.Accounts;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.SignIn;

/// <summary>Checks email and password and returns an access token.</summary>
public sealed record SignInCommand(string Email, string Password) : ICommand<SignInResponse>;
