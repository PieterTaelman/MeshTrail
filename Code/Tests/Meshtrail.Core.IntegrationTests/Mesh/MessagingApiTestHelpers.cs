using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Mesh.Contacts;
using static Meshtrail.Core.IntegrationTests.Mesh.MeshApiTestHelpers;

namespace Meshtrail.Core.IntegrationTests.Mesh;

internal static partial class MessagingApiTestHelpers
{
    public const string RegistrationsUrl = "/api/v1/registrations";
    public const string MessagesUrl = "/api/v1/messages";

    public static byte[] Key(byte seed) => [.. Enumerable.Range(0, 32).Select(i => (byte)(seed + i))];

    /// <summary>The link the Meshtastic app would show for this node.</summary>
    public static string ContactLink(uint nodeNum, byte[] publicKey, string longName = "Hiker") => ContactUrl.Create(new SharedContact
    {
        NodeNum = nodeNum,
        User = new User { LongName = longName, ShortName = "HKR", PublicKey = ByteString.CopyFrom(publicKey) },
    });

    public static async Task<RegistrationDto> RegisterAsync(HttpClient client, uint nodeNum, byte[] publicKey)
    {
        var response = await client.PostAsJsonAsync($"{RegistrationsUrl}/from-contact-url", new RegisterFromContactUrlRequest(ContactLink(nodeNum, publicKey)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RegistrationDto>())!;
    }

    /// <summary>Reads the code out of the direct message the API sent to the node (what the user sees on its screen).</summary>
    public static async Task<string> SentCodeAsync(uint nodeNum)
    {
        var packet = await Radio.WaitForSentPacketAsync(sent => sent.To == nodeNum && sent.Decoded.Portnum == PortNum.TextMessageApp);
        return SixDigits().Match(packet.Decoded.Payload.ToStringUtf8()).Value;
    }

    /// <summary>A delivery report from <paramref name="from"/> for our packet.</summary>
    public static FromRadio RoutingReport(uint from, uint requestId, Routing.Types.Error error = Routing.Types.Error.None) =>
        Packet(from, PortNum.RoutingApp, new Routing { ErrorReason = error }, requestId);

    public static FromRadio TextFrom(uint from, uint to, string text, uint packetId)
    {
        var message = Packet(from, PortNum.TextMessageApp, new Position(), 0);
        message.Packet.To = to;
        message.Packet.Id = packetId;
        message.Packet.Decoded.Payload = ByteString.CopyFromUtf8(text);
        return message;
    }

    /// <summary>Waits until the message with this id shows the expected status in its conversation or channel.</summary>
    public static Task<MessageDto> WaitForStatusAsync(HttpClient client, string listUrl, Guid messageId, string status) =>
        EventuallyAsync(
            async () => (await TryGetAsync<PagedResult<MessageDto>>(client, listUrl))?.Items.FirstOrDefault(message => message.Id == messageId),
            message => message.Status == status);

    [GeneratedRegex("[0-9]{6}")]
    private static partial Regex SixDigits();
}
