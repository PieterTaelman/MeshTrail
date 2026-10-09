using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh;

/// <summary>Finds the gateway to reach a node through (see <see cref="GatewayRouting"/> for the rule).</summary>
internal static class GatewayRoutes
{
    public static GatewayRoute ToRoute(this MeshGateway gateway) => new(
        gateway.NodeNum ?? throw new InvalidOperationException("A pending gateway cannot send."),
        gateway.Transport,
        gateway.Broker,
        gateway.MqttUserName,
        gateway.MqttRoot,
        gateway.DownlinkChannel);

    /// <summary>Load who heard the node → keep the gateways we can send with → pick the best one. 422 when there is none.</summary>
    public static async Task<GatewayRoute> PickAsync(
        INodeReceptionRepository receptions,
        IMeshGatewayRepository gateways,
        uint nodeNum,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var heard = await receptions.GetForNodeAsync(nodeNum, cancellationToken);
        var candidates = await gateways.GetActiveByNodeNumsAsync([.. heard.Select(reception => reception.GatewayNodeNum).Distinct()], cancellationToken);
        var sendable = candidates.Where(gateway => gateway.CanSend).ToDictionary(gateway => gateway.NodeNum!.Value);

        var best = GatewayRouting.PickOrThrow(nodeNum, heard, sendable.Keys.ToHashSet(), now);
        return sendable[best.GatewayNodeNum].ToRoute();
    }
}
