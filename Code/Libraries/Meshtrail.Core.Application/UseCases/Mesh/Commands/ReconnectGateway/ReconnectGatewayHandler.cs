using Mediator;
using Meshtrail.Core.Application.Abstractions;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.ReconnectGateway;

public sealed class ReconnectGatewayHandler(IMeshGateway meshGateway) : ICommandHandler<ReconnectGatewayCommand>
{
    public ValueTask<Unit> Handle(ReconnectGatewayCommand command, CancellationToken cancellationToken)
    {
        meshGateway.RequestReconnect();
        return ValueTask.FromResult(Unit.Value);
    }
}
