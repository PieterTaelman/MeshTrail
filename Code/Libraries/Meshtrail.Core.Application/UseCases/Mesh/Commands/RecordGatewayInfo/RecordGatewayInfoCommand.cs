using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordGatewayInfo;

/// <summary>The gateway node told us its own node number and/or firmware version.</summary>
public sealed record RecordGatewayInfoCommand(uint? NodeNum, string? FirmwareVersion) : ICommand;
