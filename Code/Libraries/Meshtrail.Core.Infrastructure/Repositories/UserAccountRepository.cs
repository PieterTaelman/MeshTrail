using Meshtrail.Core.Application.Common;
using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Accounts;
using Meshtrail.Core.Infrastructure.Persistence;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Meshtrail.Core.Infrastructure.Repositories;

internal sealed class UserAccountRepository(MeshtrailDbContext dbContext) : IUserAccountRepository
{
    private readonly Dictionary<UserAccount, DbUserAccount> _rows = new(ReferenceEqualityComparer.Instance);

    private DbSet<DbUserAccount> Accounts => dbContext.Set<DbUserAccount>();

    public async Task<UserAccount?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Track(await Accounts.FirstOrDefaultAsync(row => row.Id == id, cancellationToken));

    public async Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        Track(await Accounts.FirstOrDefaultAsync(row => row.Email == email, cancellationToken));

    public Task AddAsync(UserAccount account, CancellationToken cancellationToken)
    {
        var row = new DbUserAccount { Id = account.Id };
        CopyTo(account, row);
        Accounts.Add(row);
        _rows[account] = row;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(UserAccount account, CancellationToken cancellationToken)
    {
        if (!_rows.TryGetValue(account, out var row))
        {
            row = new DbUserAccount { Id = account.Id, RowVersion = account.RowVersion };
            Accounts.Attach(row);
            _rows[account] = row;
        }

        CopyTo(account, row);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyException("The account was changed at the same time. Try again.", exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Two registrations with the same address at the same moment.
            throw new ConcurrencyException("This email address was just registered. Try signing in.", exception);
        }

        foreach (var (account, row) in _rows)
        {
            if (dbContext.Entry(row).State != EntityState.Detached)
            {
                account.SyncRowVersion(row.RowVersion);
            }
        }
    }

    private UserAccount? Track(DbUserAccount? row)
    {
        if (row is null)
        {
            return null;
        }

        var account = UserAccount.Rehydrate(
            row.Id,
            row.Email,
            row.FirstName,
            row.LastName,
            row.PasswordHash,
            row.EmailConfirmedAt,
            row.ConfirmationTokenHash,
            row.ConfirmationExpiresAt,
            row.ResetTokenHash,
            row.ResetExpiresAt,
            row.FailedSignIns,
            row.LockedUntil,
            row.CreatedAt,
            row.RowVersion);
        _rows[account] = row;
        return account;
    }

    private static void CopyTo(UserAccount account, DbUserAccount row)
    {
        row.Email = account.Email;
        row.FirstName = account.FirstName;
        row.LastName = account.LastName;
        row.PasswordHash = account.PasswordHash;
        row.EmailConfirmedAt = account.EmailConfirmedAt;
        row.ConfirmationTokenHash = account.ConfirmationTokenHash;
        row.ConfirmationExpiresAt = account.ConfirmationExpiresAt;
        row.ResetTokenHash = account.ResetTokenHash;
        row.ResetExpiresAt = account.ResetExpiresAt;
        row.FailedSignIns = account.FailedSignIns;
        row.LockedUntil = account.LockedUntil;
        row.CreatedAt = account.CreatedAt;
    }
}
