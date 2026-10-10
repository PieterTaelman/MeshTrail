using FluentValidation;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.RequestPasswordReset;

public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(256);
    }
}
