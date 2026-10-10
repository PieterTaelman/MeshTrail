using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Accounts;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.ResendConfirmation;

public sealed class ResendConfirmationHandler(
    IUserAccountRepository accounts,
    ISecureTokenGenerator tokens,
    IEmailSender mail,
    IClientLinks links,
    TimeProvider timeProvider) : ICommandHandler<ResendConfirmationCommand>
{
    public async ValueTask<Unit> Handle(ResendConfirmationCommand command, CancellationToken cancellationToken)
    {
        var account = await accounts.GetByEmailAsync(UserAccount.NormalizeEmail(command.Email), cancellationToken);
        if (account is null || account.IsConfirmed)
        {
            return Unit.Value;
        }

        var token = tokens.NewToken();
        account.IssueConfirmation(token, timeProvider.GetUtcNow());
        await accounts.UpdateAsync(account, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
        await mail.SendAsync(AccountMails.Confirm(account, links.ConfirmEmail(account.Id, token)), cancellationToken);
        return Unit.Value;
    }
}
