using FluentValidation;
using Meshtrail.Core.Domain.Accounts;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.UpdateProfile;

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(UserAccount.NameMaxLength);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(UserAccount.NameMaxLength);
    }
}
