using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Domain.Accounts;

namespace Meshtrail.Core.Application.UseCases.Accounts;

/// <summary>The texts of the account mails. Plain text, so they look the same in every mail program.</summary>
internal static class AccountMails
{
    public static EmailMessage Confirm(UserAccount account, string link) => new(
        account.Email,
        "Confirm your Meshtrail account",
        $"""
        Hello {account.FirstName},

        Please confirm your email address to finish your Meshtrail registration:

        {link}

        The link works for {UserAccount.ConfirmationLifetime.TotalHours:0} hours. Did you not register? Then ignore this mail.
        """);

    public static EmailMessage AlreadyRegistered(UserAccount account, string signInLink) => new(
        account.Email,
        "You already have a Meshtrail account",
        $"""
        Hello {account.FirstName},

        Someone (probably you) tried to register again with this address. You already have an account:

        {signInLink}

        Forgot your password? Use "Forgot password" on the sign-in page.
        """);

    public static EmailMessage Reset(UserAccount account, string link) => new(
        account.Email,
        "Reset your Meshtrail password",
        $"""
        Hello {account.FirstName},

        Choose a new password with this link:

        {link}

        The link works for {UserAccount.ResetLifetime.TotalMinutes:0} minutes. Did you not ask for this? Then ignore this mail.
        """);
}
