using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meshtrail.Core.Infrastructure.Persistence.Configurations;

/// <summary>Maps DbNodePosition to dbo.NodePositions (NodePositions.sql).</summary>
internal sealed class DbNodePositionConfiguration : IEntityTypeConfiguration<DbNodePosition>
{
    public void Configure(EntityTypeBuilder<DbNodePosition> builder)
    {
        builder.ToTable("NodePositions", "dbo");
        builder.HasKey(position => position.Id).IsClustered(false);
        builder.Property(position => position.Id).ValueGeneratedNever();
    }
}
