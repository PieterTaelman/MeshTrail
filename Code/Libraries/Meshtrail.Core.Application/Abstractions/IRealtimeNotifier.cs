namespace Meshtrail.Core.Application.Abstractions;

/// <summary>
/// Pushes small "something changed" messages to connected clients. The WebApi implements it with SignalR.
/// </summary>
public interface IRealtimeNotifier
{
    Task NotifyAllAsync(string eventName, object payload, CancellationToken cancellationToken);

    /// <summary>Only to these users (all their open browsers). Nothing happens for an empty list.</summary>
    Task NotifyUsersAsync(string eventName, object payload, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken);

    /// <summary>
    /// Only to clients watching the map area around this point. At world scale most changes happen far away from
    /// what a user looks at, so this keeps the traffic per browser small.
    /// </summary>
    Task NotifyAreaAsync(string eventName, object payload, double latitude, double longitude, CancellationToken cancellationToken);
}
