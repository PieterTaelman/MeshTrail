using Meshtrail.Core.Domain.Common;

namespace Meshtrail.Core.Domain.Mesh;

/// <summary>
/// Picks the gateway to reach a node through. A packet sent by a gateway only travels a few hops, so we can only
/// reach a node through a gateway that heard it recently.
/// </summary>
public static class GatewayRouting
{
    /// <summary>A gateway that heard the node longer ago than this is not used: the node has probably moved.</summary>
    public static readonly TimeSpan ReachWindow = TimeSpan.FromHours(6);

    /// <summary>Receptions this recent are preferred over older ones, whatever their signal.</summary>
    public static readonly TimeSpan FreshWindow = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The best reception through a gateway we can send with: fresh ones first, then fewest hops, best signal and
    /// most recent. Null when no usable gateway heard the node within <see cref="ReachWindow"/>.
    /// </summary>
    public static NodeReception? Pick(IEnumerable<NodeReception> receptions, IReadOnlySet<uint> sendableGateways, DateTimeOffset now) =>
        receptions
            .Where(reception => sendableGateways.Contains(reception.GatewayNodeNum) && now - reception.LastHeardAt <= ReachWindow)
            .OrderByDescending(reception => now - reception.LastHeardAt <= FreshWindow)
            .ThenBy(reception => reception.HopsAway ?? int.MaxValue)
            .ThenByDescending(reception => reception.Snr ?? double.MinValue)
            .ThenByDescending(reception => reception.LastHeardAt)
            .FirstOrDefault();

    /// <summary>Like <see cref="Pick"/>, but a node nobody can reach is a business-rule error (HTTP 422).</summary>
    public static NodeReception PickOrThrow(uint nodeNum, IEnumerable<NodeReception> receptions, IReadOnlySet<uint> sendableGateways, DateTimeOffset now) =>
        Pick(receptions, sendableGateways, now)
        ?? throw new DomainException(
            $"No gateway can reach node {MeshNode.FormatNodeId(nodeNum)} right now: no online gateway heard it in the last {ReachWindow.TotalHours:0} hours.");
}
