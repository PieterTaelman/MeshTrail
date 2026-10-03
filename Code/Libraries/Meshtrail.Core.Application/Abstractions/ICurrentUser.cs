namespace Meshtrail.Core.Application.Abstractions;

/// <summary>
/// Who is making the current request. Inject this into handlers instead of touching HttpContext,
/// so handlers stay testable and also work from background jobs.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Stable user id from the identity provider, or null when nobody is signed in.</summary>
    string? Id { get; }

    /// <summary>Name to store in audit columns such as CreatedBy. Never empty.</summary>
    string Name { get; }

    bool IsAuthenticated { get; }

    /// <summary>Id that ties logs and traces of one request together.</summary>
    string CorrelationId { get; }
}
