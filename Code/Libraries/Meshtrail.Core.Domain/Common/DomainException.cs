namespace Meshtrail.Core.Domain.Common;

/// <summary>
/// Thrown when a business rule is broken. The API turns it into HTTP 422 so callers see the rule that failed.
/// </summary>
public sealed class DomainException(string message) : Exception(message);
