using Google.Protobuf;
using Meshtastic.Protobufs;

namespace Meshtrail.Mesh.Contacts;

/// <summary>
/// Reads and writes the "share contact" link of the Meshtastic app (the text inside its QR code):
/// https://meshtastic.org/v/#&lt;base64url of a SharedContact protobuf&gt;.
/// </summary>
public static class ContactUrl
{
    public const string Prefix = "https://meshtastic.org/v/#";

    private const string Host = "meshtastic.org";
    private const int PublicKeyLength = 32;

    /// <summary>Longest link we bother to decode; real ones are about 120 characters.</summary>
    public const int MaxLength = 1000;

    /// <summary>Parses a contact link. Returns false with a user-friendly <paramref name="error"/> when it is not one.</summary>
    public static bool TryParse(string? url, out SharedContact contact, out string error)
    {
        contact = new SharedContact();
        error = string.Empty;

        var text = url?.Trim() ?? string.Empty;
        if (text.Length == 0 || text.Length > MaxLength
            || !Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.TrimEnd('/') != "/v"
            || uri.Fragment.Length < 2)
        {
            error = $"Not a Meshtastic contact link. It should start with {Prefix}";
            return false;
        }

        if (!TryDecodeBase64Url(uri.Fragment[1..], out var bytes))
        {
            error = "The contact link is damaged (the part after # is not valid base64).";
            return false;
        }

        try
        {
            contact = SharedContact.Parser.ParseFrom(bytes);
        }
        catch (InvalidProtocolBufferException)
        {
            error = "The contact link is damaged (it does not contain a Meshtastic contact).";
            return false;
        }

        if (contact.NodeNum == 0 || contact.User is null)
        {
            error = "The contact link has no node number or user.";
            return false;
        }

        if (contact.User.PublicKey.Length is not (0 or PublicKeyLength))
        {
            error = $"The contact's public key must be {PublicKeyLength} bytes.";
            return false;
        }

        return true;
    }

    /// <summary>Builds the link the Meshtastic app would show for this contact (used by tests and the simulator).</summary>
    public static string Create(SharedContact contact) =>
        Prefix + Convert.ToBase64String(contact.ToByteArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryDecodeBase64Url(string value, out byte[] bytes)
    {
        // The app strips padding and uses the URL-safe alphabet; put both back for the standard decoder.
        var base64 = Uri.UnescapeDataString(value).TrimEnd('=').Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');

        bytes = new byte[base64.Length];
        if (!Convert.TryFromBase64String(base64, bytes, out var written))
        {
            bytes = [];
            return false;
        }

        bytes = bytes[..written];
        return true;
    }
}
