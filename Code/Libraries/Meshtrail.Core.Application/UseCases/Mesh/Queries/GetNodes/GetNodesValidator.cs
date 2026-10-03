using FluentValidation;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodes;

public sealed class GetNodesValidator : AbstractValidator<GetNodesQuery>
{
    /// <summary>Upper bound so one request cannot pull the whole table.</summary>
    public const int MaxPageSize = 500;

    public GetNodesValidator()
    {
        RuleFor(query => query.Request.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.Request.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(query => query.Request.Search).MaximumLength(100);
    }
}
