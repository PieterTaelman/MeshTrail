using Mediator;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordGatewayInfo;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeHeard;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeInfo;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeUser;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.ReceiveTextMessage;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordPosition;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordRoutingResult;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordTelemetry;
using Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordTracerouteResult;
using Meshtrail.Mesh.Events;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>Maps radio events (Meshtrail.Mesh) to application commands. Pure function, so it is easy to unit test.</summary>
internal static class MeshEventCommands
{
    public static IMessage? ToCommand(MeshEvent meshEvent) => meshEvent switch
    {
        GatewayInfoReceived info => new RecordGatewayInfoCommand(info.NodeNum, info.FirmwareVersion),
        NodeInfoReceived node => new RecordNodeInfoCommand(
            node.NodeNum,
            ToUser(node.User),
            ToPosition(node.Position),
            node.Telemetry is { } telemetry ? new RadioTelemetry(telemetry.BatteryLevel, telemetry.Voltage) : null,
            node.LastHeardAt,
            node.Snr,
            node.HopsAway,
            node.ReceivedAt),
        NodeHeard heard => new RecordNodeHeardCommand(heard.NodeNum, heard.ReceivedAt, heard.Snr, heard.Rssi, heard.HopsAway),
        NodeUserReceived user => new RecordNodeUserCommand(user.NodeNum, ToUser(user.User)!, user.ReceivedAt),
        PositionReceived position => new RecordPositionCommand(position.NodeNum, ToPosition(position.Position)!, position.ReceivedAt),
        TelemetryReceived telemetry => new RecordTelemetryCommand(
            telemetry.NodeNum, new RadioTelemetry(telemetry.Telemetry.BatteryLevel, telemetry.Telemetry.Voltage), telemetry.ReceivedAt),
        TracerouteReceived route => new RecordTracerouteResultCommand(
            route.NodeNum, route.RequestId, route.RouteTowards, route.SnrTowards, route.RouteBack, route.SnrBack),
        TextReceived text => new ReceiveTextMessageCommand(
            text.From, text.To, text.Channel, text.Text, text.PacketId, text.Snr, text.Rssi, text.HopsAway, text.ReceivedAt),
        RoutingReceived routing => new RecordRoutingResultCommand(routing.From, routing.RequestId, routing.Error),
        // ConfigCompleted and future events without a use case: nothing to do.
        _ => null,
    };

    private static RadioUser? ToUser(NodeUser? user) =>
        user is null ? null : new RadioUser(user.LongName, user.ShortName, user.HardwareModel, user.Role, user.PublicKey);

    private static RadioPosition? ToPosition(PositionReport? position) =>
        position is null ? null : new RadioPosition(position.Latitude, position.Longitude, position.Altitude, position.Time, position.PrecisionBits);
}
