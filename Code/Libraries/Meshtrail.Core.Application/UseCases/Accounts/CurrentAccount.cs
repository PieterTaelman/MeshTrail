using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Accounts;

namespace Meshtrail.Core.Application.UseCases.Accounts;

internal static class CurrentAccount
{
    /// <summary>The signed-in user's account. 404 when the user has no local account (e.g. the development user).</summary>
    public static async Task<UserAccount> LoadAsync(IUserAccountRepository accounts, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        Guid.TryParse(currentUser.Id, out var id) && await accounts.GetAsync(id, cancellationToken) is { } account
            ? account
            : throw new KeyNotFoundException("You have no Meshtrail account.");
}
