using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Domain.Accounts;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using IdentityHasher = Microsoft.AspNetCore.Identity.PasswordHasher<Meshtrail.Core.Domain.Accounts.UserAccount>;
using IdentityResult = Microsoft.AspNetCore.Identity.PasswordVerificationResult;

namespace Meshtrail.Core.Infrastructure.Accounts;

/// <summary>ASP.NET Core Identity's password hasher (PBKDF2 with a versioned format, so it can be upgraded later).</summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly IdentityHasher _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public PasswordCheck Verify(string hash, string password) => _hasher.VerifyHashedPassword(null!, hash, password) switch
    {
        IdentityResult.Success => PasswordCheck.Right,
        IdentityResult.SuccessRehashNeeded => PasswordCheck.RightButOutdated,
        _ => PasswordCheck.Wrong,
    };
}

/// <summary>Signs access tokens (JWT, HMAC-SHA256) that the API itself validates (Authentication:Mode = Local).</summary>
public sealed class JwtAccessTokenIssuer(IOptions<LocalAuthOptions> options, TimeProvider timeProvider) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(UserAccount account)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expires = now + settings.TokenLifetime;
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, account.DisplayName),
                new Claim(JwtRegisteredClaimNames.Email, account.Email),
            ]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(settings.SigningKeyBytes()), SecurityAlgorithms.HmacSha256),
        });
        return new AccessToken(token, expires);
    }
}

/// <summary>32 random bytes, URL-safe, for mail links.</summary>
public sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    public string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
}

/// <summary>Links into the web client (Authentication:Local:ClientBaseUrl).</summary>
public sealed class ClientLinks(IOptions<LocalAuthOptions> options) : IClientLinks
{
    private string Base => options.Value.ClientBaseUrl.TrimEnd('/');

    public string ConfirmEmail(Guid userId, string token) => $"{Base}/account/confirm?user={userId}&token={Uri.EscapeDataString(token)}";

    public string ResetPassword(Guid userId, string token) => $"{Base}/account/reset?user={userId}&token={Uri.EscapeDataString(token)}";

    public string SignIn() => $"{Base}/account/sign-in";
}
