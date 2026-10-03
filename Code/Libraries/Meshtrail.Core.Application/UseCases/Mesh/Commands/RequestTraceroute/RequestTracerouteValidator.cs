using FluentValidation;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestTraceroute;

public sealed class RequestTracerouteValidator : AbstractValidator<RequestTracerouteCommand>
{
    public RequestTracerouteValidator()
    {
        RuleFor(command => command.NodeNum)
            .NotEqual(0u)
            .NotEqual(uint.MaxValue).WithMessage("'Node Num' must be a single node, not the broadcast address.");
    }
}
