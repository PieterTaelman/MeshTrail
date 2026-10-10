using FluentValidation;
using Meshtrail.Core.Domain.Accounts;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.Register;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(UserAccount.EmailMaxLength);
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(UserAccount.NameMaxLength);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(UserAccount.NameMaxLength);
        RuleFor(command => command.Password).NotEmpty().MinimumLength(UserAccount.PasswordMinLength).MaximumLength(200);
    }
}
