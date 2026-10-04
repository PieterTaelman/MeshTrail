using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.FailTimedOutMessages;

/// <summary>Marks messages sent before <paramref name="SentBefore"/> without a delivery report as Failed. Returns the count.</summary>
public sealed record FailTimedOutMessagesCommand(DateTimeOffset SentBefore) : ICommand<int>;
