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
        RuleFor(command => command.ChannelIndex).InclusiveBetween(0, MeshMessage.MaxChannelIndex);
        RuleFor(command => command.ToNodeNum)
            .NotEqual(0u)
            .NotEqual(uint.MaxValue).WithMessage("'To Node Num' must be a single node; leave it empty to send to the channel.")
            .When(command => command.ToNodeNum is not null);
    }
}
