using FluentValidation;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.AddGateway;

public sealed class AddGatewayValidator : AbstractValidator<AddGatewayCommand>
{
    public AddGatewayValidator()
    {
        RuleFor(command => command.NodeNum)
            .NotEqual(0u)
            .NotEqual(uint.MaxValue).WithMessage("'Node Num' must be a single node, not the broadcast address.");
    }
}
