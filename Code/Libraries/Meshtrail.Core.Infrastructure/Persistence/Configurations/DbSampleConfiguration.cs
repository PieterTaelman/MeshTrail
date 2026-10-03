using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meshtrail.Core.Infrastructure.Persistence.Configurations;

/// <summary>Maps DbSample to dbo.Samples. Must match Code/Database/Meshtrail.Database/dbo/Tables/Samples.sql.</summary>
internal sealed class DbSampleConfiguration : IEntityTypeConfiguration<DbSample>
{
    public void Configure(EntityTypeBuilder<DbSample> builder)
    {
        builder.ToTable("Samples", "dbo");
        builder.HasKey(sample => sample.Id).IsClustered(false);

        // The domain creates the id (GUID v7), so the database must not generate one.
        builder.Property(sample => sample.Id).ValueGeneratedNever();
        builder.Property(sample => sample.Name).HasMaxLength(200).IsRequired();
        builder.Property(sample => sample.Description).HasMaxLength(2000);
        builder.Property(sample => sample.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(sample => sample.ModifiedBy).HasMaxLength(256);

        // Tells EF to add "WHERE RowVersion = @original" to updates/deletes and to read the new value back.
        builder.Property(sample => sample.RowVersion).IsRowVersion();
    }
}
