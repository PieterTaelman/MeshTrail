namespace Meshtrail.Mesh.Mqtt;

/// <summary>What kind of message a Meshtastic MQTT topic carries.</summary>
public enum MeshtasticTopicKind
{
    /// <summary><c>&lt;root&gt;/2/e/&lt;channel&gt;/&lt;gatewayId&gt;</c>: a protobuf ServiceEnvelope (one mesh packet).</summary>
    Envelope,

    /// <summary><c>&lt;root&gt;/2/json/&lt;channel&gt;/&lt;gatewayId&gt;</c>: the same packet as JSON (only when the node has JSON on).</summary>
    Json,

    /// <summary><c>&lt;root&gt;/2/map/</c>: a MapReport (the node advertises itself for public maps).</summary>
    Map,

    /// <summary><c>&lt;root&gt;/2/stat/&lt;gatewayId&gt;</c>: "online" / "offline" (last will) of a gateway.</summary>
    Status,
}

/// <summary>
/// A parsed Meshtastic MQTT topic. The root is configurable on each node (default "msh/EU_868" style), so we find the
/// "/2/&lt;kind&gt;/" part and treat everything before it as the root.
/// </summary>
public sealed record MeshtasticTopic(string Root, MeshtasticTopicKind Kind, string? Channel, string? GatewayId)
{
    private const string ProtocolVersion = "2";

    public static bool TryParse(string? topic, out MeshtasticTopic parsed)
    {
        parsed = new MeshtasticTopic(string.Empty, MeshtasticTopicKind.Envelope, null, null);
        if (string.IsNullOrWhiteSpace(topic))
        {
            return false;
        }

        var segments = topic.Split('/');

        // Search from the end: a root could itself contain a "2" segment.
        for (var i = segments.Length - 2; i >= 1; i--)
        {
            if (segments[i] != ProtocolVersion || KindOf(segments[i + 1]) is not { } kind)
            {
                continue;
            }

            var root = string.Join('/', segments[..i]);
            var rest = segments[(i + 2)..].Where(segment => segment.Length > 0).ToArray();
            parsed = kind switch
            {
                MeshtasticTopicKind.Envelope or MeshtasticTopicKind.Json when rest.Length >= 2 =>
                    new MeshtasticTopic(root, kind, rest[0], rest[1]),
                MeshtasticTopicKind.Envelope or MeshtasticTopicKind.Json when rest.Length == 1 =>
                    new MeshtasticTopic(root, kind, rest[0], null),
                MeshtasticTopicKind.Status => new MeshtasticTopic(root, kind, null, rest.FirstOrDefault()),
                _ => new MeshtasticTopic(root, kind, null, null),
            };
            return true;
        }

        return false;
    }

    /// <summary>Topic to publish a packet to: gateways with downlink on <paramref name="channel"/> pick it up.</summary>
    public static string ForEnvelope(string root, string channel, string senderId) => $"{root}/{ProtocolVersion}/e/{channel}/{senderId}";

    private static MeshtasticTopicKind? KindOf(string segment) => segment switch
    {
        "e" => MeshtasticTopicKind.Envelope,
        "json" => MeshtasticTopicKind.Json,
        "map" => MeshtasticTopicKind.Map,
        "stat" => MeshtasticTopicKind.Status,
        _ => null,
    };
}
