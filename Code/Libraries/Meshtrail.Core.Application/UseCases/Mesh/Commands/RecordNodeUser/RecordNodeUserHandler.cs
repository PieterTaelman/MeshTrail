using Mediator;
using Meshtrail.Core.Application.Repositories;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeUser;


public sealed class RecordNodeUserHandler(
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    INodeRegistrationRepository registrations,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RecordNodeUserCommand>
{
    public async ValueTask<Unit> Handle(RecordNodeUserCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var user = command.User;
        await MeshNodeUpdates.ApplyAsync(nodes, gateways, registrations, publisher, command.NodeNum, now, node =>
        {
            node.ApplyUser(user.LongName, user.ShortName, user.HardwareModel, user.Role, user.PublicKey, now);
            return Task.CompletedTask;
        }, cancellationToken);
        return Unit.Value;
    }
}

