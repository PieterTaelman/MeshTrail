using FluentValidation;

namespace Meshtrail.Core.Application.UseCases.Samples.Queries.GetSampleById;

public sealed class GetSampleByIdValidator : AbstractValidator<GetSampleByIdQuery>
{
    public GetSampleByIdValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}
