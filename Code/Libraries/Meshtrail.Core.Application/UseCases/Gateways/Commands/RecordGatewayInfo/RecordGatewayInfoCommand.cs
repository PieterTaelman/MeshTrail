using Mediator;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayInfo;

/// <summary>A TCP gateway told us its firmware version (right after connecting).</summary>
public sealed record RecordGatewayInfoCommand(uint GatewayNodeNum, string? FirmwareVersion) : ICommand;
