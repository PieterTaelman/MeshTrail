using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordRoutingResult;

/// <summary>A delivery report for one of our packets. Error "None" means acknowledged.</summary>
public sealed record RecordRoutingResultCommand(uint From, uint RequestId, string Error) : ICommand;
