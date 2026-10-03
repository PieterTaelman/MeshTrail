using FluentValidation;
using Meshtrail.Core.Contracts.Samples;

namespace Meshtrail.Core.Application.UseCases.Samples.Queries.GetSampleGrid;

public sealed class GetSampleGridValidator : AbstractValidator<GetSampleGridQuery>
{
    /// <summary>Upper bound so one request cannot pull the whole table.</summary>
    public const int MaxPageSize = 200;

    public GetSampleGridValidator()
    {
        RuleFor(query => query.Request.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.Request.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(query => query.Request.Search).MaximumLength(200);
        RuleFor(query => query.Request.SortBy)
            .Must(sortBy => sortBy is null || SampleGridSortColumns.All.Contains(sortBy))
            .WithMessage($"'Sort By' must be one of: {string.Join(", ", SampleGridSortColumns.All)}.");
    }
}
