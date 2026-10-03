namespace Meshtrail.Core.Application.Common;

/// <summary>
/// Someone else saved the same record first. Infrastructure throws it instead of the EF exception,
/// so nothing outside Infrastructure needs EF. The API turns it into HTTP 409.
/// </summary>
public sealed class ConcurrencyException(string message, Exception? innerException = null)
    : Exception(message, innerException);
