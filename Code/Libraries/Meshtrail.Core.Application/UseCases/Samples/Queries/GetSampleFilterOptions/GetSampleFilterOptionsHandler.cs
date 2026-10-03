using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Samples;

namespace Meshtrail.Core.Application.UseCases.Samples.Queries.GetSampleFilterOptions;

public sealed class GetSampleFilterOptionsHandler(ISampleRepository repository)
    : IQueryHandler<GetSampleFilterOptionsQuery, SampleFilterOptionsDto>
{
    public async ValueTask<SampleFilterOptionsDto> Handle(GetSampleFilterOptionsQuery query, CancellationToken cancellationToken) =>
        await repository.GetFilterOptionsAsync(cancellationToken);
}
