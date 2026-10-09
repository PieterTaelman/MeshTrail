using Mediator;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayUplink;

/// <summary>
/// Packets arrived through a gateway (sent by the ingest about once a minute per gateway, and on a new channel).
/// MQTT: MqttUserName is the login the broker stamped on the uplink. Returns false when the uplink must be ignored
/// (unknown or revoked login, or a login that belongs to another node).
/// </summary>
public sealed record RecordGatewayUplinkCommand(
    GatewayTransport Transport,
    uint GatewayNodeNum,
    string? MqttUserName,
    string? Broker,
    string? MqttRoot,
    string? ChannelName,
    DateTimeOffset ReceivedAt) : ICommand<bool>;
