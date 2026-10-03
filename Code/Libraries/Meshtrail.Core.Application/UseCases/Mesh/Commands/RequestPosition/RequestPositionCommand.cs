using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RequestPosition;

/// <summary>Ask a node to send its current position. The answer arrives later as a NodeUpdated push.</summary>
public sealed record RequestPositionCommand(uint NodeNum) : ICommand;
