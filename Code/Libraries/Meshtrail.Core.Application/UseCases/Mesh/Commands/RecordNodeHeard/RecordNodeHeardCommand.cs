using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeHeard;

/// <summary>Sent by the gateway worker for every packet: the node is alive and in range.</summary>
public sealed record RecordNodeHeardCommand(uint NodeNum, DateTimeOffset HeardAt, double? Snr, int? Rssi, int? HopsAway) : ICommand;
