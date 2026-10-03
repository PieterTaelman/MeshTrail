using Mediator;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestTraceroute;

/// <summary>Ask for the route to a node. Returns the pending traceroute; the result arrives as a TracerouteCompleted push.</summary>
public sealed record RequestTracerouteCommand(uint NodeNum) : ICommand<NodeTracerouteDto>;
