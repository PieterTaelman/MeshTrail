using FluentValidation;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.RevokeGateway;

public sealed class RevokeGatewayValidator : AbstractValidator<RevokeGatewayCommand>
{
    public RevokeGatewayValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}
