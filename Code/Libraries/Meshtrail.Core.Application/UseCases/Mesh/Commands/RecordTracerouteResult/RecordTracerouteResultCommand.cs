using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordTracerouteResult;

/// <summary>The answer to a traceroute arrived (RequestId = the packet id we sent).</summary>
public sealed record RecordTracerouteResultCommand(
    uint NodeNum,
    uint RequestId,
    IReadOnlyList<uint> RouteTowards,
    IReadOnlyList<double?> SnrTowards,
    IReadOnlyList<uint> RouteBack,
    IReadOnlyList<double?> SnrBack) : ICommand;
