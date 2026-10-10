using Meshtrail.Core.Domain.Accounts;

namespace Meshtrail.Core.Application.Abstractions;

public enum PasswordCheck
{
    Wrong,
    Right,

    /// <summary>Right, but stored in an older format: store the new hash.</summary>
    RightButOutdated,
}

/// <summary>Hashes and checks passwords. Implemented in Infrastructure (the algorithm is not a business rule).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordCheck Verify(string hash, string password);
}

/// <summary>A signed access token for the API (and SignalR).</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface IAccessTokenIssuer
{
    AccessToken Issue(UserAccount account);
}

/// <summary>Random tokens for mail links (confirmation, password reset).</summary>
public interface ISecureTokenGenerator
{
    string NewToken();
}

/// <summary>A plain-text mail.</summary>
public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>Sends mail (SMTP, or the log when no mail server is configured).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Links into the web client that mails point to.</summary>
public interface IClientLinks
{
    string ConfirmEmail(Guid userId, string token);

    string ResetPassword(Guid userId, string token);

    string SignIn();
}
