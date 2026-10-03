using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordTelemetry;

/// <summary>A node reported its battery and voltage.</summary>
public sealed record RecordTelemetryCommand(uint NodeNum, RadioTelemetry Telemetry, DateTimeOffset ReceivedAt) : ICommand;
