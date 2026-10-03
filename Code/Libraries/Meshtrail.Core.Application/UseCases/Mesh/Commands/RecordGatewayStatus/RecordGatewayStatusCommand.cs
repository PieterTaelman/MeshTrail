using Mediator;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordGatewayStatus;

/// <summary>The gateway worker connected, lost the connection or is trying again. Mode = "Tcp" or "Simulated".</summary>
public sealed record RecordGatewayStatusCommand(GatewayStatus Status, string Mode, string? Error) : ICommand;
