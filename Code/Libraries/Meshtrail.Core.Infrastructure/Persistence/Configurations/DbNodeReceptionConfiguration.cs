using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meshtrail.Core.Infrastructure.Persistence.Configurations;

/// <summary>Maps DbNodeReception to dbo.NodeReceptions (NodeReceptions.sql).</summary>
internal sealed class DbNodeReceptionConfiguration : IEntityTypeConfiguration<DbNodeReception>
{
    public void Configure(EntityTypeBuilder<DbNodeReception> builder)
    {
        builder.ToTable("NodeReceptions", "dbo");
        builder.HasKey(reception => new { reception.NodeNum, reception.GatewayNodeNum });

        // Declared so EF inserts a newly discovered node before its first reception in the same save.
        builder.HasOne<DbMeshNode>().WithMany().HasForeignKey(reception => reception.NodeNum);
    }
}
