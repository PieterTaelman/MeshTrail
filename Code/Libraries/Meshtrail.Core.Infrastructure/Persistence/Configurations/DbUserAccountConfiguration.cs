using Meshtrail.Core.Domain.Accounts;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meshtrail.Core.Infrastructure.Persistence.Configurations;

/// <summary>Maps DbUserAccount to dbo.UserAccounts (UserAccounts.sql).</summary>
internal sealed class DbUserAccountConfiguration : IEntityTypeConfiguration<DbUserAccount>
{
    public void Configure(EntityTypeBuilder<DbUserAccount> builder)
    {
        builder.ToTable("UserAccounts", "dbo");
        builder.HasKey(account => account.Id).IsClustered(false);
        builder.Property(account => account.Id).ValueGeneratedNever();
        builder.Property(account => account.Email).HasMaxLength(UserAccount.EmailMaxLength).IsRequired();
        builder.Property(account => account.FirstName).HasMaxLength(UserAccount.NameMaxLength).IsRequired();
        builder.Property(account => account.LastName).HasMaxLength(UserAccount.NameMaxLength).IsRequired();
        builder.Property(account => account.PasswordHash).HasMaxLength(400).IsRequired();
        builder.Property(account => account.ConfirmationTokenHash).HasMaxLength(32);
        builder.Property(account => account.ResetTokenHash).HasMaxLength(32);
        builder.Property(account => account.RowVersion).IsRowVersion();
    }
}
