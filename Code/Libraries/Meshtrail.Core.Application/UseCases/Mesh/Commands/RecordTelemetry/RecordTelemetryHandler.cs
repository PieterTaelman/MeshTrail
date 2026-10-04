using Mediator;
using Meshtrail.Core.Application.Repositories;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordTelemetry;


public sealed class RecordTelemetryHandler(
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    INodeRegistrationRepository registrations,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RecordTelemetryCommand>
{
    public async ValueTask<Unit> Handle(RecordTelemetryCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await MeshNodeUpdates.ApplyAsync(nodes, gateways, registrations, publisher, command.NodeNum, now, node =>
        {
            node.RecordTelemetry(command.Telemetry.BatteryLevel, command.Telemetry.Voltage, now);
            return Task.CompletedTask;
        }, cancellationToken);
        return Unit.Value;
    }
}

