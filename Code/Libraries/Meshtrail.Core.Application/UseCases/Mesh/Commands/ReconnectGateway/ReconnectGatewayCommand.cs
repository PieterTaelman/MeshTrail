using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.ReconnectGateway;

/// <summary>Drop the gateway connection and connect again now (e.g. after the phone app let go of the node).</summary>
public sealed record ReconnectGatewayCommand : ICommand;
