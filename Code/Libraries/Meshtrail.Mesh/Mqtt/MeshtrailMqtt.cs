using System.Text.Json;
using MQTTnet;
using MQTTnet.Packets;

namespace Meshtrail.Mesh.Mqtt;

/// <summary>
/// Names the Meshtrail broker and the API agree on. MQTT 5 "user properties" travel with a message, so the broker
/// can tell the API which gateway login sent an uplink, and the API can tell the broker which gateway a downlink is for.
/// </summary>
public static class MeshtrailMqtt
{
    /// <summary>Set by the broker on every gateway uplink: the gateway's login. Gateways cannot fake it (the broker overwrites it).</summary>
    public const string GatewayProperty = "meshtrail-gateway";

    /// <summary>Set by the API on a downlink: only the gateway with this login receives it.</summary>
    public const string TargetProperty = "meshtrail-target";

    /// <summary>The broker publishes which gateway logins are connected here (only service logins receive it).</summary>
    public const string ConnectionsTopic = "meshtrail/broker/gateways";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Value of a user property, or null when the message does not have it.</summary>
    public static string? GetProperty(MqttApplicationMessage message, string name) =>
        message.UserProperties?.FirstOrDefault(property => property.Name == name)?.ReadValueAsString();

    public static byte[] SerializeConnections(GatewayConnections connections) => JsonSerializer.SerializeToUtf8Bytes(connections, JsonOptions);

    public static GatewayConnections? DeserializeConnections(byte[] payload)
    {
        try
        {
            return JsonSerializer.Deserialize<GatewayConnections>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Snapshot of the gateway logins connected to one broker (Broker = its name, e.g. "local" or "eu-west").</summary>
public sealed record GatewayConnections(string Broker, IReadOnlyList<string> UserNames);
