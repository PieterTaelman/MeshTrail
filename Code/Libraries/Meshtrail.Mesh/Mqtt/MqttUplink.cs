using System.Text;
using Google.Protobuf;
using Meshtastic.Protobufs;

namespace Meshtrail.Mesh.Mqtt;

/// <summary>
/// Something a gateway published. Envelope is set for <see cref="MeshtasticTopicKind.Envelope"/> topics.
/// UserName = the gateway's login (on the API side it comes from the broker's <see cref="MeshtrailMqtt.GatewayProperty"/>).
/// </summary>
public sealed record MqttUplink(
    DateTimeOffset ReceivedAt,
    string? ClientId,
    string? UserName,
    string RawTopic,
    MeshtasticTopic? Topic,
    ServiceEnvelope? Envelope,
    byte[] Payload)
{
    /// <summary>Payload as text, for JSON and status topics.</summary>
    public string PayloadText => Encoding.UTF8.GetString(Payload);

    /// <summary>Parses a received MQTT message into an uplink (topic + envelope when it is one).</summary>
    public static MqttUplink From(DateTimeOffset receivedAt, string? clientId, string? userName, string topic, byte[] payload)
    {
        var parsedTopic = MeshtasticTopic.TryParse(topic, out var parsed) ? parsed : null;
        ServiceEnvelope? envelope = null;
        if (parsedTopic?.Kind == MeshtasticTopicKind.Envelope)
        {
            try
            {
                envelope = ServiceEnvelope.Parser.ParseFrom(payload);
            }
            catch (InvalidProtocolBufferException)
            {
                // Not a valid envelope: keep the raw payload, the caller decides what to do.
            }
        }

        return new MqttUplink(receivedAt, clientId, userName, topic, parsedTopic, envelope, payload);
    }
}
