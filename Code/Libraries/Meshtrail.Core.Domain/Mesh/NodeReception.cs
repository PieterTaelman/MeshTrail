namespace Meshtrail.Core.Domain.Mesh;

/// <summary>
/// "Gateway G heard node N": the latest time, signal and hop count. One row per node and gateway. This is how we know
/// which gateway can reach a node (a LoRa packet only travels a few hops around the gateway that sends it).
/// </summary>
public sealed class NodeReception
{
    /// <summary>Clocks on small devices drift; a time further ahead than this is replaced by our own time.</summary>
    private static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(1);

    private NodeReception()
    {
    }

    public uint NodeNum { get; private set; }

    public uint GatewayNodeNum { get; private set; }

    public DateTimeOffset LastHeardAt { get; private set; }

    public double? Snr { get; private set; }

    public int? Rssi { get; private set; }

    /// <summary>0 = the gateway heard the node directly; more = relays in between.</summary>
    public int? HopsAway { get; private set; }

    public static NodeReception Record(uint nodeNum, uint gatewayNodeNum, DateTimeOffset heardAt, double? snr, int? rssi, int? hopsAway, DateTimeOffset now) => new()
    {
        NodeNum = nodeNum,
        GatewayNodeNum = gatewayNodeNum,
        LastHeardAt = heardAt > now + MaxClockSkew ? now : heardAt,
        Snr = snr,
        Rssi = rssi,
        HopsAway = hopsAway is >= 0 ? hopsAway : null,
    };

    public static NodeReception Rehydrate(uint nodeNum, uint gatewayNodeNum, DateTimeOffset lastHeardAt, double? snr, int? rssi, int? hopsAway) => new()
    {
        NodeNum = nodeNum,
        GatewayNodeNum = gatewayNodeNum,
        LastHeardAt = lastHeardAt,
        Snr = snr,
        Rssi = rssi,
        HopsAway = hopsAway,
    };

    /// <summary>A newer reception. Returns false (and changes nothing) when it is older than the one we have.</summary>
    public bool Update(DateTimeOffset heardAt, double? snr, int? rssi, int? hopsAway, DateTimeOffset now)
    {
        if (heardAt > now + MaxClockSkew)
        {
            heardAt = now;
        }

        if (heardAt < LastHeardAt)
        {
            return false;
        }

        LastHeardAt = heardAt;
        Snr = snr ?? Snr;
        Rssi = rssi ?? Rssi;
        HopsAway = hopsAway is >= 0 ? hopsAway : HopsAway;
        return true;
    }
}
