using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Mesh.Contacts;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>Adapts <see cref="ContactUrl"/> (protobuf) to the Application's <see cref="IContactUrlParser"/>.</summary>
internal sealed class ContactUrlParser : IContactUrlParser
{
    public bool TryParse(string? url, out ParsedContact contact, out string error)
    {
        contact = new ParsedContact(0, null, null, null, null, []);
        if (!ContactUrl.TryParse(url, out var shared, out error))
        {
            return false;
        }

        var user = shared.User;
        contact = new ParsedContact(
            shared.NodeNum,
            NullIfEmpty(user.LongName),
            NullIfEmpty(user.ShortName),
            user.HwModel.ToString(),
            user.Role.ToString(),
            user.PublicKey.ToByteArray());
        return true;
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
