using Mediator;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Common;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.ConfirmEmail;

public sealed class ConfirmEmailHandler(IUserAccountRepository accounts, TimeProvider timeProvider) : ICommandHandler<ConfirmEmailCommand>
{
    public const string InvalidLink = "This link is not valid or has expired. Ask for a new confirmation mail on the sign-in page.";

    public async ValueTask<Unit> Handle(ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(command.UserId, cancellationToken);
        if (account is null || !account.ConfirmEmail(command.Token, timeProvider.GetUtcNow()))
        {
            throw new DomainException(InvalidLink);
        }

        await accounts.UpdateAsync(account, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
