using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.MarkMessageSent;

/// <summary>The gateway put the message on the air; now we wait for the delivery report.</summary>
public sealed record MarkMessageSentCommand(Guid MessageId) : ICommand;
