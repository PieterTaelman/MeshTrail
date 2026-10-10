using Meshtrail.Core.Contracts.Accounts;
using Meshtrail.Core.Domain.Accounts;

namespace Meshtrail.Core.Application.UseCases.Accounts;

internal static class AccountMappings
{
    public static ProfileDto ToDto(this UserAccount account) =>
        new(account.Id, account.Email, account.FirstName, account.LastName, account.DisplayName, account.CreatedAt);
}
