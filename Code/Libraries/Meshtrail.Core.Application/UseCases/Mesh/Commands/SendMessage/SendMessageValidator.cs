using System.Text;
using FluentValidation;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.SendMessage;

public sealed class SendMessageValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageValidator()
    {
        RuleFor(command => command.Text)
            .NotEmpty()
            .Must(text => Encoding.UTF8.GetByteCount(text?.Trim() ?? string.Empty) <= MeshMessage.TextMaxBytes)
            .WithMessage($"'Text' can be at most {MeshMessage.TextMaxBytes} bytes (emoji and accents take 2–4 bytes).");
        RuleFor(command => command.ToNodeNum)
            .NotEqual(0u)
            .NotEqual(uint.MaxValue).WithMessage("'To Node Num' must be a single node: there is no worldwide channel.")
            .When(command => command.ToNodeNum is not null);
        RuleFor(command => command)
            .Must(command => command.ToNodeNum is null != command.TeamId is null)
            .WithName("ToNodeNum")
            .WithMessage("Give exactly one of 'toNodeNum' (direct message) or 'teamId' (team chat).");
        RuleFor(command => command.TeamId).NotEqual(Guid.Empty).When(command => command.TeamId is not null);
    }
}
