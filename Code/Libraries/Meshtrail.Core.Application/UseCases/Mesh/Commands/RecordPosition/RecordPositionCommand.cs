using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordPosition;

/// <summary>A node sent its position (broadcast or as an answer to our request).</summary>
public sealed record RecordPositionCommand(uint NodeNum, RadioPosition Position, DateTimeOffset ReceivedAt) : ICommand;
