using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Samples;

namespace Meshtrail.Core.Application.UseCases.Samples.Queries.GetSampleById;

public sealed class GetSampleByIdHandler(ISampleRepository repository) : IQueryHandler<GetSampleByIdQuery, SampleDto>
{
    public async ValueTask<SampleDto> Handle(GetSampleByIdQuery query, CancellationToken cancellationToken)
    {
        var sample = await repository.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Sample {query.Id} does not exist.");

        return sample.ToDto();
    }
}
