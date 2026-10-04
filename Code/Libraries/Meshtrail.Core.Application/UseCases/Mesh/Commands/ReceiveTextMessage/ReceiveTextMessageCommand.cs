using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.ReceiveTextMessage;

/// <summary>A text arrived from the mesh. To = broadcast address (channel message) or a node (direct message).</summary>
public sealed record ReceiveTextMessageCommand(
    uint From,
    uint To,
    int Channel,
    string Text,
    uint PacketId,
    double? Snr,
    int? Rssi,
    int? HopsAway,
    DateTimeOffset ReceivedAt) : ICommand;
