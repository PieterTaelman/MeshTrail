using FluentValidation;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.SignIn;

public sealed class SignInValidator : AbstractValidator<SignInCommand>
{
    public SignInValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(256);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(200);
    }
}
