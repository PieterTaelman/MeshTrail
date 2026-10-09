using Mediator;
using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.SendMessage;

/// <summary>
/// Queue a direct message to ToNodeNum (through the gateway that heard it best) or a team message to TeamId (through
/// every online gateway that carries the team's channel). Exactly one of the two.
/// </summary>
public sealed record SendMessageCommand(uint? ToNodeNum, Guid? TeamId, string Text) : ICommand<MessageDto>;
