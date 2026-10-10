using FluentValidation;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.ResendConfirmation;

public sealed class ResendConfirmationValidator : AbstractValidator<ResendConfirmationCommand>
{
    public ResendConfirmationValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(256);
    }
}
