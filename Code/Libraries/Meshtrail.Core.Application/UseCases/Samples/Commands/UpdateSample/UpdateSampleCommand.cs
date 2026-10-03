using Mediator;
using Meshtrail.Core.Contracts.Samples;

namespace Meshtrail.Core.Application.UseCases.Samples.Commands.UpdateSample;

/// <summary>RowVersion is the base64 value the client got when it loaded the sample.</summary>
public sealed record UpdateSampleCommand(Guid Id, string Name, string? Description, string RowVersion) : ICommand<SampleDto>;
