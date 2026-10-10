using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Accounts;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.Register;

/// <summary>
/// New address → account + confirmation mail. Unconfirmed account → the latest registration wins (names and password
/// replaced, new link). Confirmed account → a "you already have an account" mail. The answer is always the same, so
/// nobody can find out which addresses are registered.
/// </summary>
public sealed class RegisterHandler(
    IUserAccountRepository accounts,
    IPasswordHasher passwords,
    ISecureTokenGenerator tokens,
    IEmailSender mail,
    IClientLinks links,
    TimeProvider timeProvider) : ICommandHandler<RegisterCommand>
{
    public async ValueTask<Unit> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var email = UserAccount.CheckEmail(command.Email);
        var existing = await accounts.GetByEmailAsync(email, cancellationToken);

        if (existing is { IsConfirmed: true })
        {
            await mail.SendAsync(AccountMails.AlreadyRegistered(existing, links.SignIn()), cancellationToken);
            return Unit.Value;
        }

        var token = tokens.NewToken();
        UserAccount account;
        if (existing is null)
        {
            account = UserAccount.Register(email, command.FirstName, command.LastName, passwords.Hash(command.Password), token, now);
            await accounts.AddAsync(account, cancellationToken);
        }
        else
        {
            account = existing;
            account.Rename(command.FirstName, command.LastName);
            account.UpgradePasswordHash(passwords.Hash(command.Password));
            account.IssueConfirmation(token, now);
            await accounts.UpdateAsync(account, cancellationToken);
        }

        await accounts.SaveChangesAsync(cancellationToken);
        await mail.SendAsync(AccountMails.Confirm(account, links.ConfirmEmail(account.Id, token)), cancellationToken);
        return Unit.Value;
    }
}
