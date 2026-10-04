using Mediator;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.SendMessage;

/// <summary>Queue a text: direct message when ToNodeNum is set, else a broadcast on ChannelIndex (default 0).</summary>
public sealed record SendMessageCommand(int? ChannelIndex, uint? ToNodeNum, string Text) : ICommand<MessageDto>;
