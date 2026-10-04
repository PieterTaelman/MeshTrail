using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meshtrail.Core.Infrastructure.Persistence.Configurations;

/// <summary>Maps DbMeshMessage to dbo.MeshMessages (MeshMessages.sql).</summary>
internal sealed class DbMeshMessageConfiguration : IEntityTypeConfiguration<DbMeshMessage>
{
    public void Configure(EntityTypeBuilder<DbMeshMessage> builder)
    {
        builder.ToTable("MeshMessages", "dbo");
        builder.HasKey(message => message.Id).IsClustered(false);

        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.Direction).HasMaxLength(10).IsRequired();
        builder.Property(message => message.Kind).HasMaxLength(20).IsRequired();
        builder.Property(message => message.Text).HasMaxLength(MeshMessage.StoredTextMaxLength).IsRequired();
        builder.Property(message => message.Status).HasMaxLength(20).IsRequired();
        builder.Property(message => message.FailureReason).HasMaxLength(MeshMessage.FailureReasonMaxLength);
        builder.Property(message => message.CreatedBy).HasMaxLength(256);
    }
}
