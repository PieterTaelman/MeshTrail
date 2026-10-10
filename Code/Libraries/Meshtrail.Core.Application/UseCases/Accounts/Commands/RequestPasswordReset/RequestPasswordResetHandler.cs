using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Accounts;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.RequestPasswordReset;

public sealed class RequestPasswordResetHandler(
    IUserAccountRepository accounts,
    ISecureTokenGenerator tokens,
    IEmailSender mail,
    IClientLinks links,
    TimeProvider timeProvider) : ICommandHandler<RequestPasswordResetCommand>
{
    public async ValueTask<Unit> Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        var account = await accounts.GetByEmailAsync(UserAccount.NormalizeEmail(command.Email), cancellationToken);
        if (account is null)
        {
            return Unit.Value;
        }

        var token = tokens.NewToken();
        account.RequestReset(token, timeProvider.GetUtcNow());
        await accounts.UpdateAsync(account, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
        await mail.SendAsync(AccountMails.Reset(account, links.ResetPassword(account.Id, token)), cancellationToken);
        return Unit.Value;
    }
}
