using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.MarkMessageSent;

/// <summary>The message went out to its gateway; now we wait for the delivery report.</summary>
public sealed record MarkMessageSentCommand(Guid MessageId) : ICommand;
