using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayUplink;

/// <summary>
/// Find the gateway (MQTT: by login; TCP/simulator: by node) → on its first uplink tie it to the node → mark it
/// online and remember its root and channel → tell the clients → send messages that waited for it.
/// </summary>
public sealed class RecordGatewayUplinkHandler(
    IMeshGatewayRepository gateways,
    IMeshNodeRepository nodes,
    IMeshMessageRepository messages,
    IMeshOutbox outbox,
    IGatewaySetup setup,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RecordGatewayUplinkCommand, bool>
{
    public const string NodeTakenError = "This node is already a gateway of another user; its uplinks are ignored.";

    public async ValueTask<bool> Handle(RecordGatewayUplinkCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var (gateway, isNew) = await FindAsync(command, now, cancellationToken);
        if (gateway is null)
        {
            return false;
        }

        var wasSending = gateway.CanSend;
        var changed = isNew;
        if (gateway.NodeNum is null)
        {
            // First uplink: the node may not already be someone else's gateway.
            if (!await ReleaseNodeAsync(gateway, command.GatewayNodeNum, now, cancellationToken))
            {
                gateway.ReportError(NodeTakenError);
                await SaveAsync(gateway, isNew, cancellationToken);
                return false;
            }

            changed = true;
        }

        var bind = gateway.Bind(command.GatewayNodeNum, now);
        if (bind is GatewayBindResult.OtherNode or GatewayBindResult.Revoked)
        {
            return false;
        }

        changed |= gateway.RecordUplink(command.MqttRoot, command.ChannelName, now);
        if (command.ChannelName is { Length: > 0 } channel)
        {
            await gateways.RecordChannelAsync(gateway.Id, channel, now, cancellationToken);
        }

        await SaveAsync(gateway, isNew, cancellationToken);

        if (changed)
        {
            await GatewayViews.PublishAsync(gateway, nodes, gateways, publisher, cancellationToken);
        }

        if (!wasSending && gateway.CanSend)
        {
            await MeshMessaging.RequeueAsync(messages, outbox, gateway, cancellationToken);
        }

        return true;
    }

    private async Task<(MeshGateway? Gateway, bool IsNew)> FindAsync(RecordGatewayUplinkCommand command, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (command.Transport != GatewayTransport.Mqtt)
        {
            // TCP / simulator: the gateway is the node itself; it registers itself on its first packet.
            var local = await gateways.GetActiveByNodeNumAsync(command.GatewayNodeNum, cancellationToken);
            return local is not null ? (local, false) : (MeshGateway.Local(command.Transport, command.GatewayNodeNum, now), true);
        }

        if (string.IsNullOrEmpty(command.MqttUserName))
        {
            return (null, false);
        }

        var registered = await gateways.GetByMqttUserNameAsync(command.MqttUserName, cancellationToken);
        if (registered is not null || !setup.AcceptUnregisteredGateways)
        {
            return (registered, false);
        }

        // Development: a login the broker accepted without asking us. It becomes an ownerless gateway,
        // unless the node already belongs to a registered gateway.
        var existing = await gateways.GetActiveByNodeNumAsync(command.GatewayNodeNum, cancellationToken);
        return existing switch
        {
            null => (MeshGateway.Unregistered(command.MqttUserName, command.GatewayNodeNum, command.Broker, now), true),
            { OwnerUserId: null, Transport: GatewayTransport.Mqtt } => (existing, false),
            _ => (null, false),
        };
    }

    /// <summary>
    /// A node is at most one active gateway. An older gateway on the same node is replaced when it has no owner or the
    /// same owner (e.g. new credentials for the same node); another user's gateway blocks.
    /// </summary>
    private async Task<bool> ReleaseNodeAsync(MeshGateway gateway, uint nodeNum, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var other = await gateways.GetActiveByNodeNumAsync(nodeNum, cancellationToken);
        if (other is null || other.Id == gateway.Id)
        {
            return true;
        }

        if (other.OwnerUserId is not null && other.OwnerUserId != gateway.OwnerUserId)
        {
            return false;
        }

        other.Revoke(now);
        await gateways.UpdateAsync(other, cancellationToken);

        // Save the revoke first: the database allows only one active gateway per node.
        await gateways.SaveChangesAsync(cancellationToken);
        await GatewayViews.PublishAsync(other, nodes, gateways, publisher, cancellationToken);
        return true;
    }

    private async Task SaveAsync(MeshGateway gateway, bool isNew, CancellationToken cancellationToken)
    {
        if (isNew)
        {
            await gateways.AddAsync(gateway, cancellationToken);
        }
        else
        {
            await gateways.UpdateAsync(gateway, cancellationToken);
        }

        await gateways.SaveChangesAsync(cancellationToken);
    }
}
