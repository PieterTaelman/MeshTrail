using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.RecordNodeUser;

/// <summary>A node broadcast its user info (names, hardware, public key).</summary>
public sealed record RecordNodeUserCommand(uint NodeNum, RadioUser User, DateTimeOffset ReceivedAt) : ICommand;
