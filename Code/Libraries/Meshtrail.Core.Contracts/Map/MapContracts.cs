namespace Meshtrail.Core.Contracts.Map;

/// <summary>
/// Query-string parameters of GET map/features.
/// Bbox = "west,south,east,north" in degrees (optional). Layers = comma-separated layer names (optional = all).
/// </summary>
public sealed record MapFeaturesRequest
{
    public string? Bbox { get; init; }

    public string? Layers { get; init; }
}

/// <summary>Layer names known today. New layers (tracking, external feeds) add a name here and a layer source.</summary>
public static class MapLayers
{
    public const string Nodes = "nodes";

    public const string Gateways = "gateways";
}

/// <summary>GeoJSON FeatureCollection (RFC 7946), so map libraries can use it as-is.</summary>
public sealed record MapFeatureCollectionDto(IReadOnlyList<MapFeatureDto> Features)
{
    public string Type => "FeatureCollection";
}

/// <summary>
/// GeoJSON Feature. Properties always contain "layer" and "source"; the rest depends on the layer.
/// Id is unique across layers, e.g. "nodes:4044729068".
/// </summary>
public sealed record MapFeatureDto(string Id, MapPointDto Geometry, IReadOnlyDictionary<string, object?> Properties)
{
    public string Type => "Feature";
}

/// <summary>GeoJSON Point: Coordinates = [longitude, latitude] (longitude first, as GeoJSON requires).</summary>
public sealed record MapPointDto(IReadOnlyList<double> Coordinates)
{
    public string Type => "Point";
}
