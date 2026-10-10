using Asp.Versioning;
using Mediator;
using Meshtrail.Core.Application.UseCases.Map.Queries.GetMapFeatures;
using Meshtrail.Core.Contracts.Map;
using Microsoft.AspNetCore.Mvc;

namespace Meshtrail.WebApi.Controllers;

/// <summary>Map layers as GeoJSON, e.g. GET map/features?bbox=2.5,49.5,6.4,51.5&amp;layers=nodes.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/map")]
public sealed class MapController(ISender sender) : ControllerBase
{
    /// <summary>Public: the map can be viewed without an account.</summary>
    [HttpGet("features")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<ActionResult<MapFeatureCollectionDto>> GetFeatures([FromQuery] MapFeaturesRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetMapFeaturesQuery(request), cancellationToken));
}
