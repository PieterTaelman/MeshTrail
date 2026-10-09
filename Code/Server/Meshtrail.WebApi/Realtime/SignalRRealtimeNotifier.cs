using Meshtrail.Core.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Meshtrail.WebApi.Realtime;

/// <summary>SignalR implementation of <see cref="IRealtimeNotifier"/>; keeps SignalR out of the Application layer.</summary>
internal sealed class SignalRRealtimeNotifier(IHubContext<NotificationsHub> hubContext) : IRealtimeNotifier
{
    public Task NotifyAllAsync(string eventName, object payload, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync(eventName, payload, cancellationToken);

    public Task NotifyUsersAsync(string eventName, object payload, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken) =>
        userIds.Count == 0 ? Task.CompletedTask : hubContext.Clients.Users(userIds).SendAsync(eventName, payload, cancellationToken);

    public Task NotifyAreaAsync(string eventName, object payload, double latitude, double longitude, CancellationToken cancellationToken) =>
        hubContext.Clients.Groups(MapAreas.GroupsFor(latitude, longitude)).SendAsync(eventName, payload, cancellationToken);
}
