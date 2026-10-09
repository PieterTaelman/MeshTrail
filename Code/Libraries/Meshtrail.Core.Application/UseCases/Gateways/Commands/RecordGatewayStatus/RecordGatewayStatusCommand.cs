using Mediator;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayStatus;

/// <summary>A TCP or simulated gateway connected or lost its connection.</summary>
public sealed record RecordGatewayStatusCommand(GatewayTransport Transport, uint GatewayNodeNum, bool Connected, string? Error) : ICommand;
