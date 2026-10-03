using Mediator;
using Meshtrail.Core.Contracts.Map;

namespace Meshtrail.Core.Application.UseCases.Map.Queries.GetMapFeatures;

public sealed record GetMapFeaturesQuery(MapFeaturesRequest Request) : IQuery<MapFeatureCollectionDto>;
