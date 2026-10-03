using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Samples;

namespace Meshtrail.Core.Application.UseCases.Samples.Queries.GetSampleGrid;

public sealed class GetSampleGridHandler(ISampleRepository repository)
    : IQueryHandler<GetSampleGridQuery, PagedResult<SampleGridItemDto>>
{
    public async ValueTask<PagedResult<SampleGridItemDto>> Handle(GetSampleGridQuery query, CancellationToken cancellationToken) =>
        await repository.GetGridAsync(query.Request, cancellationToken);
}
