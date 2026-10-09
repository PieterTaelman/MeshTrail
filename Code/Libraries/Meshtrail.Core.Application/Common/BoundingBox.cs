using System.Globalization;

namespace Meshtrail.Core.Application.Common;

/// <summary>A map rectangle in degrees. West may be greater than East when the box crosses the 180° meridian.</summary>
public sealed record BoundingBox(double West, double South, double East, double North)
{
    /// <summary>Is the point inside? Handles boxes that cross the 180° meridian.</summary>
    public bool Contains(double latitude, double longitude) =>
        latitude >= South && latitude <= North
        && (West <= East ? longitude >= West && longitude <= East : longitude >= West || longitude <= East);

    /// <summary>Parses "west,south,east,north" (the order map libraries use). Returns false for anything else.</summary>
    public static bool TryParse(string? text, out BoundingBox box)
    {
        box = new BoundingBox(-180, -90, 180, 90);
        var parts = text?.Split(',', StringSplitOptions.TrimEntries);
        if (parts is not { Length: 4 })
        {
            return false;
        }

        var values = new double[4];
        for (var i = 0; i < 4; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || double.IsNaN(values[i]))
            {
                return false;
            }
        }

        var (west, south, east, north) = (values[0], values[1], values[2], values[3]);
        if (west is < -180 or > 180 || east is < -180 or > 180 || south is < -90 or > 90 || north is < -90 or > 90 || south > north)
        {
            return false;
        }

        box = new BoundingBox(west, south, east, north);
        return true;
    }
}
