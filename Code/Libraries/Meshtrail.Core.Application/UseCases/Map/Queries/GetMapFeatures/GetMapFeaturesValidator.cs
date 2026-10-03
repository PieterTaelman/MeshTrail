using FluentValidation;
using Meshtrail.Core.Application.Common;

namespace Meshtrail.Core.Application.UseCases.Map.Queries.GetMapFeatures;

public sealed class GetMapFeaturesValidator : AbstractValidator<GetMapFeaturesQuery>
{
    public GetMapFeaturesValidator(IEnumerable<IMapLayerSource> layers)
    {
        var known = layers.Select(layer => layer.Layer).ToHashSet(StringComparer.OrdinalIgnoreCase);

        RuleFor(query => query.Request.Bbox)
            .Must(bbox => bbox is null || BoundingBox.TryParse(bbox, out _))
            .WithMessage("'Bbox' must be \"west,south,east,north\" in degrees.");
        RuleFor(query => query.Request.Layers)
            .MaximumLength(200)
            .Must(value => SplitLayers(value).All(known.Contains))
            .WithMessage($"'Layers' may only contain: {string.Join(", ", known.Order())}.");
    }

    /// <summary>"nodes, beacons" → {"nodes", "beacons"}; empty = all layers.</summary>
    public static IReadOnlySet<string> SplitLayers(string? layers) =>
        (layers ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(layer => layer.ToLowerInvariant())
            .ToHashSet();
}
