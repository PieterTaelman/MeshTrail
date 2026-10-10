using Mediator;
using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Accounts;

namespace Meshtrail.Core.Application.UseCases.Accounts.Queries.GetMyProfile;

public sealed class GetMyProfileHandler(IUserAccountRepository accounts, ICurrentUser currentUser) : IQueryHandler<GetMyProfileQuery, ProfileDto>
{
    public async ValueTask<ProfileDto> Handle(GetMyProfileQuery query, CancellationToken cancellationToken) =>
        (await CurrentAccount.LoadAsync(accounts, currentUser, cancellationToken)).ToDto();
}
