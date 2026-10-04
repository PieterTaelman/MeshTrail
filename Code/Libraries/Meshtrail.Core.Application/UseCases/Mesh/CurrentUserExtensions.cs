using Meshtrail.Core.Application.Abstractions;

namespace Meshtrail.Core.Application.UseCases.Mesh;

internal static class CurrentUserExtensions
{
    /// <summary>The stable user id, falling back to the name when the identity provider gives no id.</summary>
    public static string StableId(this ICurrentUser user) => string.IsNullOrWhiteSpace(user.Id) ? user.Name : user.Id;
}
