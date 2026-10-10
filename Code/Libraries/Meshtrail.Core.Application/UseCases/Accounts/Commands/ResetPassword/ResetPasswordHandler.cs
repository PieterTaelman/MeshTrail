using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Common;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.ResetPassword;

public sealed class ResetPasswordHandler(IUserAccountRepository accounts, IPasswordHasher passwords, TimeProvider timeProvider)
    : ICommandHandler<ResetPasswordCommand>
{
    public const string InvalidLink = "This link is not valid or has expired. Ask for a new one with \"Forgot password\".";

    public async ValueTask<Unit> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(command.UserId, cancellationToken);
        if (account is null || !account.ResetPassword(command.Token, passwords.Hash(command.Password), timeProvider.GetUtcNow()))
        {
            throw new DomainException(InvalidLink);
        }

        await accounts.UpdateAsync(account, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
