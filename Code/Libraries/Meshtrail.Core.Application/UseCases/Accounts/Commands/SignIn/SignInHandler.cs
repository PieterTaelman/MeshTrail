using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Accounts;
using Meshtrail.Core.Domain.Accounts;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.SignIn;

/// <summary>
/// Find the account → check the password → record the attempt (wrong ones count towards the lock-out) → token.
/// Every attempt is saved before answering.
/// </summary>
public sealed class SignInHandler(
    IUserAccountRepository accounts,
    IPasswordHasher passwords,
    IAccessTokenIssuer tokens,
    TimeProvider timeProvider) : ICommandHandler<SignInCommand, SignInResponse>
{
    public const string WrongCredentials = "Wrong email or password.";
    public const string NotConfirmed = "Confirm your email address first: click the link in the mail we sent you.";

    public async ValueTask<SignInResponse> Handle(SignInCommand command, CancellationToken cancellationToken)
    {
        var account = await accounts.GetByEmailAsync(UserAccount.NormalizeEmail(command.Email), cancellationToken)
            ?? throw new SignInFailedException(SignInResult.WrongPassword, WrongCredentials);

        var now = timeProvider.GetUtcNow();
        var check = passwords.Verify(account.PasswordHash, command.Password);
        var result = account.SignIn(check != PasswordCheck.Wrong, now);
        if (result == SignInResult.Ok && check == PasswordCheck.RightButOutdated)
        {
            account.UpgradePasswordHash(passwords.Hash(command.Password));
        }

        await accounts.UpdateAsync(account, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);

        return result switch
        {
            SignInResult.Ok => ToResponse(account, tokens.Issue(account)),
            SignInResult.NotConfirmed => throw new SignInFailedException(result, NotConfirmed),
            SignInResult.Locked => throw new SignInFailedException(
                result, $"Too many wrong passwords. Try again in {Math.Max(1, (int)Math.Ceiling(((account.LockedUntil ?? now) - now).TotalMinutes))} minutes."),
            _ => throw new SignInFailedException(result, WrongCredentials),
        };
    }

    private static SignInResponse ToResponse(UserAccount account, AccessToken token) => new(token.Token, token.ExpiresAt, account.ToDto());
}
