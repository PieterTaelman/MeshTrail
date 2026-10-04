using FluentValidation;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.VerifyRegistration;

public sealed class VerifyRegistrationValidator : AbstractValidator<VerifyRegistrationCommand>
{
    public VerifyRegistrationValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .Matches($"^\\s*[0-9]{{{NodeRegistration.CodeLength}}}\\s*$")
            .WithMessage($"'Code' must be {NodeRegistration.CodeLength} digits.");
    }
}
