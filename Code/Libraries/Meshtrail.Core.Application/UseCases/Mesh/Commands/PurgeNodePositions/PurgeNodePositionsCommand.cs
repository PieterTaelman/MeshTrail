using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.PurgeNodePositions;

/// <summary>Deletes position history older than <paramref name="Before"/>. Returns how many rows were removed.</summary>
public sealed record PurgeNodePositionsCommand(DateTimeOffset Before) : ICommand<int>;
