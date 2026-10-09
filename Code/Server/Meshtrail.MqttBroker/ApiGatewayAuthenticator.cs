using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Meshtrail.Mesh.Mqtt;
using Microsoft.Extensions.Options;

namespace Meshtrail.MqttBroker;

/// <summary>Settings from MqttBroker:Api: where the broker checks gateway logins.</summary>
internal sealed class ApiAuthOptions
{
    public const string SectionName = "MqttBroker:Api";
    public const string HttpClientName = "meshtrail-api";

    /// <summary>Base address of the Meshtrail API. "https+http://api" = found by service discovery (Aspire).</summary>
    public string Url { get; set; } = "https+http://api";

    /// <summary>A valid login is remembered this long, so a reconnecting gateway does not hit the API every time.</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Asks the Meshtrail API whether a gateway login is valid (POST api/v1/mqtt/auth with the service key). The API owns
/// the gateway registry; the broker keeps no credentials itself, so it can run per region later. Answers are cached
/// briefly (keyed by a hash, never the password itself).
/// </summary>
internal sealed partial class ApiGatewayAuthenticator(
    IHttpClientFactory httpClientFactory,
    IOptions<MeshtasticMqttBrokerOptions> brokerOptions,
    IOptions<ApiAuthOptions> apiOptions,
    TimeProvider timeProvider,
    ILogger<ApiGatewayAuthenticator> logger)
{
    public const string ServiceKeyHeader = "X-Meshtrail-Service-Key";

    /// <summary>A refused login is remembered only briefly: the user may have just added the gateway.</summary>
    private static readonly TimeSpan RefusedCacheDuration = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, CachedAnswer> _cache = new();

    public async Task<bool> AuthenticateAsync(GatewayLogin login, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(login.UserName) || string.IsNullOrEmpty(login.Password))
        {
            return false;
        }

        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{login.UserName}\n{login.Password}")));
        var now = timeProvider.GetUtcNow();
        if (_cache.TryGetValue(key, out var cached) && cached.Until > now)
        {
            return cached.Allowed;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/mqtt/auth")
        {
            Content = JsonContent.Create(new AuthRequest(login.ClientId, login.UserName, login.Password)),
        };
        request.Headers.Add(ServiceKeyHeader, brokerOptions.Value.ServicePassword);

        using var response = await httpClientFactory.CreateClient(ApiAuthOptions.HttpClientName).SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var allowed = (await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken))?.Allowed == true;

        _cache[key] = new CachedAnswer(allowed, now + (allowed ? apiOptions.Value.CacheDuration : RefusedCacheDuration));
        LogChecked(login.UserName, allowed);
        return allowed;
    }

    private sealed record AuthRequest(string ClientId, string UserName, string Password);

    private sealed record AuthResponse(bool Allowed);

    private sealed record CachedAnswer(bool Allowed, DateTimeOffset Until);

    [LoggerMessage(Level = LogLevel.Information, Message = "Gateway login {UserName} checked with the API: allowed = {Allowed}")]
    private partial void LogChecked(string userName, bool allowed);
}
