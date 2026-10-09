using System.Security.Cryptography;
using Meshtrail.Core.Application.Abstractions;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary>
/// Gateway logins: "gw-" + 10 random characters, and a 24-character random password. Short enough for the node's
/// MQTT settings (the firmware keeps at most 31 characters of password), long enough that guessing is hopeless.
/// </summary>
public sealed class GatewayCredentialGenerator : IGatewayCredentialGenerator
{
    // No look-alikes (0/O, 1/l/I), so people can type the values from the screen.
    private const string UserNameAlphabet = "abcdefghjkmnpqrstuvwxyz23456789";
    private const string PasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";

    public GatewayCredentials NewCredentials() =>
        new("gw-" + RandomNumberGenerator.GetString(UserNameAlphabet, 10), RandomNumberGenerator.GetString(PasswordAlphabet, 24));
}
