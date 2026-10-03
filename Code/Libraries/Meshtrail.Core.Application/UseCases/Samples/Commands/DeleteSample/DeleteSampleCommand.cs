using Mediator;

namespace Meshtrail.Core.Application.UseCases.Samples.Commands.DeleteSample;

public sealed record DeleteSampleCommand(Guid Id) : ICommand;
