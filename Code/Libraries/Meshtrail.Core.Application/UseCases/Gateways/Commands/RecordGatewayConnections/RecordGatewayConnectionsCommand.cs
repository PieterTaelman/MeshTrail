using Mediator;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayConnections;

/// <summary>A broker told us which gateway logins are connected to it right now.</summary>
public sealed record RecordGatewayConnectionsCommand(string Broker, IReadOnlyCollection<string> ConnectedUserNames) : ICommand;
