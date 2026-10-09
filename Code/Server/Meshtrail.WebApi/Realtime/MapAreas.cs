using System.Globalization;

namespace Meshtrail.WebApi.Realtime;

/// <summary>
/// The world is cut into 5° × 5° tiles; each tile is a SignalR group. A browser joins the tiles its map shows, so it
/// only gets changes nearby. A view with too many tiles (zoomed out) joins the "world" group instead.
/// </summary>
internal static class MapAreas
{
    public const double TileDegrees = 5;
    public const int MaxTiles = 48;
    public const string WorldGroup = "area:world";

    /// <summary>The groups a change at this point goes to: its tile and the world group.</summary>
    public static IReadOnlyList<string> GroupsFor(double latitude, double longitude) =>
        [TileGroup(Row(latitude), Column(longitude)), WorldGroup];

    /// <summary>The groups a browser watching this box joins. Handles boxes that cross the 180° meridian.</summary>
    public static IReadOnlyList<string> GroupsForBox(double west, double south, double east, double north)
    {
        south = Math.Clamp(south, -90, 90);
        north = Math.Clamp(north, -90, 90);
        if (double.IsNaN(west + south + east + north) || south > north)
        {
            return [];
        }

        var columns = Columns(Math.Clamp(west, -180, 180), Math.Clamp(east, -180, 180)).ToList();
        var rows = Enumerable.Range(Row(south), Row(north) - Row(south) + 1).ToList();
        if (columns.Count * rows.Count > MaxTiles)
        {
            return [WorldGroup];
        }

        return [.. rows.SelectMany(row => columns.Select(column => TileGroup(row, column)))];
    }

    private static IEnumerable<int> Columns(double west, double east)
    {
        var last = Column(180 - 1e-9);
        if (west <= east)
        {
            return Enumerable.Range(Column(west), Column(east) - Column(west) + 1);
        }

        // Crossing the 180° meridian: from west to the edge, then from the other edge to east.
        return Enumerable.Range(Column(west), last - Column(west) + 1).Concat(Enumerable.Range(0, Column(east) + 1));
    }

    private static int Row(double latitude) => (int)Math.Floor((Math.Min(latitude, 90 - 1e-9) + 90) / TileDegrees);

    private static int Column(double longitude) => (int)Math.Floor((Math.Min(longitude, 180 - 1e-9) + 180) / TileDegrees);

    private static string TileGroup(int row, int column) => string.Create(CultureInfo.InvariantCulture, $"area:{row}:{column}");
}
