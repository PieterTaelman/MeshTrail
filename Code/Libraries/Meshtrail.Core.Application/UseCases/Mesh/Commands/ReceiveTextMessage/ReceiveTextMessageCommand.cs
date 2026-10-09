using Mediator;

namespace Meshtrail.Core.Application.UseCases.Mesh.Commands.ReceiveTextMessage;

/// <summary>
/// A text arrived through a gateway. To = broadcast address (channel message) or one of our addresses (a direct
/// message to Meshtrail; the ingest drops direct messages between other nodes).
/// </summary>
public sealed record ReceiveTextMessageCommand(
    uint From,
    uint To,
    int Channel,
    string? ChannelName,
    uint GatewayNodeNum,
    string Text,
    uint PacketId,
    double? Snr,
    int? Rssi,
    int? HopsAway,
    DateTimeOffset ReceivedAt) : ICommand;
