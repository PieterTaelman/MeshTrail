using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Contracts.Map;

namespace Meshtrail.Core.Application.UseCases.Map;

/// <summary>
/// One layer of the operations map. To add a layer (tracking, external feeds …), implement this and register it
/// in AddApplication; the map endpoint and the client pick it up by name.
/// </summary>
public interface IMapLayerSource
{
    /// <summary>Name used in ?layers=… and in each feature's "layer" property.</summary>
    string Layer { get; }

    Task<IReadOnlyList<MapFeatureDto>> GetFeaturesAsync(BoundingBox? box, CancellationToken cancellationToken);
}
