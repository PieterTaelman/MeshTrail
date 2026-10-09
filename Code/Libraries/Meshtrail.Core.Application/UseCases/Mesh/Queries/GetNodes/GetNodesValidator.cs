using FluentValidation;
using Meshtrail.Core.Application.Common;

namespace Meshtrail.Core.Application.UseCases.Mesh.Queries.GetNodes;

public sealed class GetNodesValidator : AbstractValidator<GetNodesQuery>
{
    /// <summary>Upper bound so one request cannot pull the whole table.</summary>
    public const int MaxPageSize = 500;

    /// <summary>The only owner filter today: nodes registered to the current user.</summary>
    public const string OwnerMe = "me";

    public GetNodesValidator()
    {
        RuleFor(query => query.Request.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.Request.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(query => query.Request.Search).MaximumLength(100);
        RuleFor(query => query.Request.Bbox)
            .Must(bbox => bbox is null || BoundingBox.TryParse(bbox, out _))
            .WithMessage("'Bbox' must be \"west,south,east,north\" in degrees.");
        RuleFor(query => query.Request.Owner)
            .Must(owner => owner is null || string.Equals(owner, OwnerMe, StringComparison.OrdinalIgnoreCase))
            .WithMessage($"'Owner' can only be \"{OwnerMe}\".");
    }
}
