using FluentValidation;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestPosition;

public sealed class RequestPositionValidator : AbstractValidator<RequestPositionCommand>
{
    public RequestPositionValidator()
    {
        RuleFor(command => command.NodeNum)
            .NotEqual(0u)
            .NotEqual(uint.MaxValue).WithMessage("'Node Num' must be a single node, not the broadcast address.");
    }
}
