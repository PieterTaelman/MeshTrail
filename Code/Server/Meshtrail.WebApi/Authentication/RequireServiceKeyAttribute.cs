using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Meshtrail.WebApi.Authentication;

/// <summary>
/// Only lets a request through when its X-Meshtrail-Service-Key header equals Meshtastic:Mqtt:Password (the service
/// password the broker also knows). Used for the broker's internal calls; compared in constant time.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class RequireServiceKeyAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Meshtrail-Service-Key";
    public const string ConfigurationKey = "Meshtastic:Mqtt:Password";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expected = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()[ConfigurationKey];
        var given = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(expected)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(expected)))
        {
            context.Result = new UnauthorizedResult();
        }
    }
}
