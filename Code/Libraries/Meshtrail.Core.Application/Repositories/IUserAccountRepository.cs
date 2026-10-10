using Meshtrail.Core.Domain.Accounts;

namespace Meshtrail.Core.Application.Repositories;

/// <summary>Storage for <see cref="UserAccount"/>. SaveChangesAsync throws ConcurrencyException on a conflict.</summary>
public interface IUserAccountRepository
{
    Task<UserAccount?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>By (normalised) email address.</summary>
    Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    Task AddAsync(UserAccount account, CancellationToken cancellationToken);

    Task UpdateAsync(UserAccount account, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
