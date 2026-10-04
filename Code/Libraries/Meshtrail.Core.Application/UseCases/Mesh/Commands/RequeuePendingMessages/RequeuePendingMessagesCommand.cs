using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RequeuePendingMessages;

/// <summary>Hands every still-Queued message to the gateway again (after a restart or reconnect). Returns the count.</summary>
public sealed record RequeuePendingMessagesCommand : ICommand<int>;
