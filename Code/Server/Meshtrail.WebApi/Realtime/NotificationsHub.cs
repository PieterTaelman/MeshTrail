using Microsoft.AspNetCore.SignalR;

namespace Meshtrail.WebApi.Realtime;

/// <summary>
/// One hub for server-to-client pushes. It has no methods on purpose: clients only listen,
/// they change data through the REST API.
/// </summary>
public sealed class NotificationsHub : Hub
{
    public const string Path = "/hubs/notifications";
}
