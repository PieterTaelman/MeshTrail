using Meshtrail.Core.Domain.Common;

namespace Meshtrail.Core.Domain.Mesh;

/// <summary>
/// A point on earth (WGS84). Time = when the fix was taken. PrecisionBits comes from Meshtastic:
/// 32 = exact, lower values mean the sender deliberately blurred its position (0 = unknown).
/// </summary>
public sealed record GeoPosition
{
    private GeoPosition(double latitude, double longitude, int? altitude, DateTimeOffset time, int precisionBits)
    {
        Latitude = latitude;
        Longitude = longitude;
        Altitude = altitude;
        Time = time;
        PrecisionBits = precisionBits;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    /// <summary>Metres above sea level, when known.</summary>
    public int? Altitude { get; }

    public DateTimeOffset Time { get; }

    public int PrecisionBits { get; }

    public static GeoPosition Create(double latitude, double longitude, int? altitude, DateTimeOffset time, int precisionBits)
    {
        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
        {
            throw new DomainException("Latitude must be between -90 and 90 degrees.");
        }

        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
        {
            throw new DomainException("Longitude must be between -180 and 180 degrees.");
        }

        return new GeoPosition(latitude, longitude, altitude, time, Math.Clamp(precisionBits, 0, 32));
    }
}
