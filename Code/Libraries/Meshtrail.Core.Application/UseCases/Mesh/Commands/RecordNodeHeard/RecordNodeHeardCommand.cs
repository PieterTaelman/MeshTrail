using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeHeard;

/// <summary>A packet from the node reached a gateway: the node is alive, and that gateway can reach it.</summary>
public sealed record RecordNodeHeardCommand(
    uint NodeNum,
    uint GatewayNodeNum,
    DateTimeOffset HeardAt,
    double? Snr,
    int? Rssi,
    int? HopsAway) : ICommand;
