namespace Meshtrail.Core.Infrastructure.Accounts;

/// <summary>Settings from Authentication:Local: our own accounts and the access tokens we sign.</summary>
public sealed class LocalAuthOptions
{
    public const string SectionName = "Authentication:Local";

    /// <summary>HMAC key for the access tokens, at least 32 characters. A secret: user secrets / environment.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "meshtrail";

    public string Audience { get; set; } = "meshtrail";

    /// <summary>How long a sign-in lasts.</summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(12);

    /// <summary>Address of the web client; mail links point here.</summary>
    public string ClientBaseUrl { get; set; } = "http://localhost:3000";

    /// <summary>The key as bytes. Throws when it is missing or too short (tokens would be easy to forge).</summary>
    public byte[] SigningKeyBytes()
    {
        if (SigningKey.Length < 32)
        {
            throw new InvalidOperationException($"Set {SectionName}:SigningKey to a secret of at least 32 characters (user secrets or environment).");
        }

        return System.Text.Encoding.UTF8.GetBytes(SigningKey);
    }
}
