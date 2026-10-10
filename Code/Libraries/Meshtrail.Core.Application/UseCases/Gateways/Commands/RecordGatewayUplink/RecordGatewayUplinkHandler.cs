using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Application.UseCases.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Gateways.Commands.RecordGatewayUplink;

/// <summary>
/// Find the gateway (MQTT: by login; TCP/simulator: by node) → only its chosen node may use the login → the first
/// uplink brings it online and registers the node to the owner (proof of ownership) → remember root and channel →
/// tell the clients → send messages that waited for it.
/// </summary>
public sealed class RecordGatewayUplinkHandler(
    IMeshGatewayRepository gateways,
    INodeRegistrationRepository registrations,
    IMeshNodeRepository nodes,
    IMeshMessageRepository messages,
    IMeshOutbox outbox,
    IGatewaySetup setup,
    TimeProvider timeProvider,
    IPublisher publisher) : ICommandHandler<RecordGatewayUplinkCommand, bool>
{
    public async ValueTask<bool> Handle(RecordGatewayUplinkCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var (gateway, isNew) = await FindAsync(command, now, cancellationToken);
        if (gateway is null)
        {
            return false;
        }

        var wasSending = gateway.CanSend;
        var bind = gateway.Bind(command.GatewayNodeNum, now);
        if (bind is GatewayBindResult.OtherNode or GatewayBindResult.Revoked)
        {
            return false;
        }

        var changed = isNew || bind == GatewayBindResult.Bound;
        changed |= gateway.RecordUplink(command.MqttRoot, command.ChannelName, now);
        if (command.ChannelName is { Length: > 0 } channel)
        {
            await gateways.RecordChannelAsync(gateway.Id, channel, now, cancellationToken);
        }

        await SaveAsync(gateway, isNew, cancellationToken);
        if (bind == GatewayBindResult.Bound && gateway.OwnerUserId is { } owner)
        {
            await RegisterNodeAsync(gateway.NodeNum!.Value, owner, gateway.OwnerName ?? owner, now, cancellationToken);
        }

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
    /// The owner put the credentials into this node and it uplinked with them: that proves the node is theirs, so it
    /// is registered to them without a code. An open claim (by anyone) gives way; a verified registration stays.
    /// </summary>
    private async Task RegisterNodeAsync(uint nodeNum, string ownerId, string ownerName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await registrations.GetActiveForNodeAsync(nodeNum, cancellationToken);
        if (existing is { Status: RegistrationStatus.Verified })
        {
            return;
        }

        if (existing is not null)
        {
            existing.Revoke("Proven by the owner's gateway login.", now);
            await registrations.UpdateAsync(existing, cancellationToken);

            // Save the revoke first: the database allows only one active registration per node.
            await registrations.SaveChangesAsync(cancellationToken);
        }

        // The gateway check runs before the uplink's packets are processed, so the node may not be known yet.
        var node = await nodes.GetAsync(nodeNum, cancellationToken);
        if (node is null)
        {
            node = MeshNode.Discover(nodeNum, now);
            await nodes.AddAsync(node, cancellationToken);
            await nodes.SaveChangesAsync(cancellationToken);
        }

        await registrations.AddAsync(
            NodeRegistration.VerifiedByGateway(nodeNum, ownerId, ownerName, node?.LongName, node?.ShortName, node?.PublicKey, now), cancellationToken);
        await registrations.SaveChangesAsync(cancellationToken);
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
