using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeInfo;

/// <summary>One entry of a TCP gateway's node database (sent right after connecting).</summary>
public sealed record RecordNodeInfoCommand(
    uint NodeNum,
    uint GatewayNodeNum,
    RadioUser? User,
    RadioPosition? Position,
    RadioTelemetry? Telemetry,
    DateTimeOffset? LastHeardAt,
    double? Snr,
    int? HopsAway,
    DateTimeOffset ReceivedAt) : ICommand;
