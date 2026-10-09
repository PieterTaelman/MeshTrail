using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeUser;

public sealed class RecordNodeUserHandler(MeshNodeStores stores, TimeProvider timeProvider, IPublisher publisher)
    : ICommandHandler<RecordNodeUserCommand>
{
    public async ValueTask<Unit> Handle(RecordNodeUserCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var user = command.User;
        await MeshNodeUpdates.ApplyAsync(stores, publisher, command.NodeNum, now, node =>
        {
            node.ApplyUser(user.LongName, user.ShortName, user.HardwareModel, user.Role, user.PublicKey, now);
            return Task.CompletedTask;
        }, cancellationToken);
        return Unit.Value;
    }
}
