using Meshtrail.Core.Application.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace Meshtrail.WebApi.Realtime;

/// <summary>SignalR implementation of <see cref="IRealtimeNotifier"/>; keeps SignalR out of the Application layer.</summary>
internal sealed class SignalRRealtimeNotifier(IHubContext<NotificationsHub> hubContext) : IRealtimeNotifier
{
    public Task NotifyAllAsync(string eventName, object payload, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync(eventName, payload, cancellationToken);
}
