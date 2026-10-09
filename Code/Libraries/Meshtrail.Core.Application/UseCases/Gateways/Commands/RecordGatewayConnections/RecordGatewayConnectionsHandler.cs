using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayConnections;

/// <summary>Every MQTT gateway of that broker is online when its login is connected, otherwise offline.</summary>
public sealed class RecordGatewayConnectionsHandler(
    IMeshGatewayRepository gateways,
    IMeshNodeRepository nodes,
    IMeshMessageRepository messages,
    IMeshOutbox outbox,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RecordGatewayConnectionsCommand>
{
    public const string NotConnectedError = "Not connected to the broker.";

    public async ValueTask<Unit> Handle(RecordGatewayConnectionsCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var connected = command.ConnectedUserNames.ToHashSet(StringComparer.Ordinal);
        var changed = new List<(MeshGateway Gateway, bool WasSending)>();

        foreach (var gateway in await gateways.GetActiveMqttOnBrokerAsync(command.Broker, cancellationToken))
        {
            var isConnected = gateway.MqttUserName is not null && connected.Contains(gateway.MqttUserName);
            var wasSending = gateway.CanSend;
            if (gateway.ChangeConnection(isConnected, isConnected ? null : NotConnectedError, now))
            {
                await gateways.UpdateAsync(gateway, cancellationToken);
                changed.Add((gateway, wasSending));
            }
        }

        if (changed.Count == 0)
        {
            return Unit.Value;
        }

        await gateways.SaveChangesAsync(cancellationToken);
        foreach (var (gateway, wasSending) in changed)
        {
            await GatewayViews.PublishAsync(gateway, nodes, gateways, publisher, cancellationToken);
            if (!wasSending && gateway.CanSend)
            {
                await MeshMessaging.RequeueAsync(messages, outbox, gateway, cancellationToken);
            }
        }

        return Unit.Value;
    }
}
