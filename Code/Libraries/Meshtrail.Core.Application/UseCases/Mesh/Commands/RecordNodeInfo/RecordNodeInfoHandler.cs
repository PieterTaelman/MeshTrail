using Mediator;
using Meshtrail.Core.Application.Repositories;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeInfo;


/// <summary>Creates or refreshes a node from the gateway's node database.</summary>
public sealed class RecordNodeInfoHandler(
    IMeshNodeRepository nodes,
    IMeshGatewayRepository gateways,
    INodeRegistrationRepository registrations,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RecordNodeInfoCommand>
{
    public async ValueTask<Unit> Handle(RecordNodeInfoCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await MeshNodeUpdates.ApplyAsync(nodes, gateways, registrations, publisher, command.NodeNum, now, async node =>
        {
            if (command.User is { } user)
            {
                node.ApplyUser(user.LongName, user.ShortName, user.HardwareModel, user.Role, user.PublicKey, now);
            }

            if (command.LastHeardAt is { } heardAt)
            {
                // RSSI is not in the node database; keep the last value we measured ourselves.
                node.RecordHeard(heardAt, command.Snr, null, command.HopsAway, now);
            }

            if (command.Telemetry is { } telemetry)
            {
                node.RecordTelemetry(telemetry.BatteryLevel, telemetry.Voltage, now);
            }

            if (command.Position is { } position)
            {
                await MeshNodeUpdates.RecordPositionAsync(nodes, node, position, command.ReceivedAt, now, cancellationToken);
            }
        }, cancellationToken);
        return Unit.Value;
    }
}

