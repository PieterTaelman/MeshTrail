using Meshtrail.Core.Domain.Accounts;

namespace Meshtrail.Core.Application.Common;

/// <summary>
/// Signing in did not work. The API answers 401 (wrong email or password), 403 (email not confirmed) or 429 (locked).
/// Wrong email and wrong password give the same message, so nobody can find out which addresses have an account.
/// </summary>
public sealed class SignInFailedException(SignInResult reason, string message) : Exception(message)
{
    public SignInResult Reason { get; } = reason;
}
