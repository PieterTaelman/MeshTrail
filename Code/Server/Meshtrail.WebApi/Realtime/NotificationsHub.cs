using Microsoft.AspNetCore.SignalR;

namespace Meshtrail.WebApi.Realtime;

/// <summary>
/// One hub for server-to-client pushes. Clients change data through the REST API; the only thing they tell the hub
/// is which map area they look at (<see cref="WatchArea"/>), so they only get node changes nearby. Anonymous
/// visitors may connect (the map is public); messages only go to signed-in users (Clients.Users).
/// </summary>
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
public sealed class NotificationsHub : Hub
{
    public const string Path = "/hubs/notifications";

    private const string AreaGroupsKey = "area-groups";

    /// <summary>Follow the nodes in this box ("west,south,east,north" in degrees). Call again whenever the map moves.</summary>
    public async Task WatchArea(double west, double south, double east, double north)
    {
        var wanted = MapAreas.GroupsForBox(west, south, east, north);
        var current = Context.Items.TryGetValue(AreaGroupsKey, out var value) && value is IReadOnlyList<string> groups ? groups : [];

        foreach (var group in current.Except(wanted))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        }

        foreach (var group in wanted.Except(current))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
        }

        Context.Items[AreaGroupsKey] = wanted;
    }
}
