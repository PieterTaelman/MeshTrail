using Mediator;
using Meshtrail.Core.Contracts.Samples;

namespace Meshtrail.Core.Application.UseCases.Samples.Queries.GetSampleFilterOptions;

public sealed record GetSampleFilterOptionsQuery : IQuery<SampleFilterOptionsDto>;
