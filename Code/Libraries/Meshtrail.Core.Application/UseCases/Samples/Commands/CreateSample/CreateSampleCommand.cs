using Mediator;
using Meshtrail.Core.Contracts.Samples;

namespace Meshtrail.Core.Application.UseCases.Samples.Commands.CreateSample;

public sealed record CreateSampleCommand(string Name, string? Description) : ICommand<SampleDto>;
