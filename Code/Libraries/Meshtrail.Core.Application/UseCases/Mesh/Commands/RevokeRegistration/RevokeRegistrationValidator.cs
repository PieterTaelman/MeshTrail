using FluentValidation;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RevokeRegistration;

public sealed class RevokeRegistrationValidator : AbstractValidator<RevokeRegistrationCommand>
{
    public RevokeRegistrationValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}
