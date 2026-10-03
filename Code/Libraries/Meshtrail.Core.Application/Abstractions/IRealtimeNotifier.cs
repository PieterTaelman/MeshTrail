namespace Meshtrail.Core.Application.Abstractions;

/// <summary>
/// Pushes small "something changed" messages to connected clients. The WebApi implements it with SignalR.
/// </summary>
public interface IRealtimeNotifier
{
    Task NotifyAllAsync(string eventName, object payload, CancellationToken cancellationToken);
}
