using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh.Events;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordTracerouteResult;


/// <summary>Stores the route on the matching traceroute. Answers to traceroutes we did not send are ignored.</summary>
public sealed class RecordTracerouteResultHandler(
    INodeTracerouteRepository traceroutes,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RecordTracerouteResultCommand>
{
    public async ValueTask<Unit> Handle(RecordTracerouteResultCommand command, CancellationToken cancellationToken)
    {
        var traceroute = await traceroutes.GetByPacketIdAsync(command.NodeNum, command.RequestId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (traceroute is null || !traceroute.Complete(command.RouteTowards, command.SnrTowards, command.RouteBack, command.SnrBack, now))
        {
            return Unit.Value;
        }

        await traceroutes.UpdateAsync(traceroute, cancellationToken);
        await traceroutes.SaveChangesAsync(cancellationToken);
        await publisher.Publish(new TracerouteCompletedNotification(traceroute.ToDto(now)), cancellationToken);
        return Unit.Value;
    }
}

