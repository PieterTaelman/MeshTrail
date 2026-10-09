using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.FailTimedOutMessages;

/// <summary>
/// Marks as Failed: messages sent before <paramref name="SentBefore"/> without a delivery report, and messages still
/// queued from before <paramref name="QueuedBefore"/> (their gateway stayed offline). Returns the count.
/// </summary>
public sealed record FailTimedOutMessagesCommand(DateTimeOffset SentBefore, DateTimeOffset QueuedBefore) : ICommand<int>;
