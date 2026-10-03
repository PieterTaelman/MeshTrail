using Mediator;
using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Contracts.Map;

namespace Meshtrail.Core.Application.UseCases.Map.Queries.GetMapFeatures;

/// <summary>Collects the features of every requested layer into one GeoJSON FeatureCollection.</summary>
public sealed class GetMapFeaturesHandler(IEnumerable<IMapLayerSource> layers)
    : IQueryHandler<GetMapFeaturesQuery, MapFeatureCollectionDto>
{
    public async ValueTask<MapFeatureCollectionDto> Handle(GetMapFeaturesQuery query, CancellationToken cancellationToken)
    {
        BoundingBox? box = BoundingBox.TryParse(query.Request.Bbox, out var parsed) ? parsed : null;
        var requested = GetMapFeaturesValidator.SplitLayers(query.Request.Layers);

        var features = new List<MapFeatureDto>();
        foreach (var layer in layers.Where(layer => requested.Count == 0 || requested.Contains(layer.Layer)))
        {
            features.AddRange(await layer.GetFeaturesAsync(box, cancellationToken));
        }

        return new MapFeatureCollectionDto(features);
    }
}
