namespace Meshtrail.Core.Application.Abstractions;

/// <summary>What a Meshtastic contact link contains. Names are untrusted (the domain cleans them).</summary>
public sealed record ParsedContact(uint NodeNum, string? LongName, string? ShortName, string? HardwareModel, string? Role, byte[] PublicKey);

/// <summary>Reads Meshtastic "share contact" links. Implemented in Infrastructure, so the Application has no protobuf.</summary>
public interface IContactUrlParser
{
    bool TryParse(string? url, out ParsedContact contact, out string error);
}
