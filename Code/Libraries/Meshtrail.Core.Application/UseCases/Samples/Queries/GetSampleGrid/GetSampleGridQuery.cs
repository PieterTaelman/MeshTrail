using Mediator;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Samples;

namespace Meshtrail.Core.Application.UseCases.Samples.Queries.GetSampleGrid;

public sealed record GetSampleGridQuery(SampleGridRequest Request) : IQuery<PagedResult<SampleGridItemDto>>;
