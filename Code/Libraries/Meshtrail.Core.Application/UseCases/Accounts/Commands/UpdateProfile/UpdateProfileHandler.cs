using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Accounts;

namespace Meshtrail.Core.Application.UseCases.Accounts.Commands.UpdateProfile;

public sealed class UpdateProfileHandler(IUserAccountRepository accounts, ICurrentUser currentUser) : ICommandHandler<UpdateProfileCommand, ProfileDto>
{
    public async ValueTask<ProfileDto> Handle(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var account = await CurrentAccount.LoadAsync(accounts, currentUser, cancellationToken);
        account.Rename(command.FirstName, command.LastName);
        await accounts.UpdateAsync(account, cancellationToken);
        await accounts.SaveChangesAsync(cancellationToken);
        return account.ToDto();
    }
}
