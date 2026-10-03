using Mediator;
using Meshtrail.Core.Contracts.Samples;

namespace Meshtrail.Core.Application.UseCases.Samples.Queries.GetSampleById;

public sealed record GetSampleByIdQuery(Guid Id) : IQuery<SampleDto>;
