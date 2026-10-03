using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meshtrail.Core.Infrastructure.Persistence.Configurations;

/// <summary>Maps DbMeshNode to dbo.MeshNodes (MeshNodes.sql).</summary>
internal sealed class DbMeshNodeConfiguration : IEntityTypeConfiguration<DbMeshNode>
{
    public void Configure(EntityTypeBuilder<DbMeshNode> builder)
    {
        builder.ToTable("MeshNodes", "dbo");
        builder.HasKey(node => node.NodeNum);

        // The radio assigns node numbers; the database must never generate one.
        builder.Property(node => node.NodeNum).ValueGeneratedNever();
        builder.Property(node => node.NodeId).HasMaxLength(MeshNode.NodeIdLength).IsRequired();
        builder.Property(node => node.LongName).HasMaxLength(MeshNode.LongNameMaxLength).IsRequired();
        builder.Property(node => node.ShortName).HasMaxLength(MeshNode.ShortNameMaxLength).IsRequired();
        builder.Property(node => node.HardwareModel).HasMaxLength(MeshNode.HardwareModelMaxLength);
        builder.Property(node => node.Role).HasMaxLength(MeshNode.RoleMaxLength);
        builder.Property(node => node.PublicKey).HasMaxLength(MeshNode.PublicKeyLength);
        builder.Property(node => node.Source).HasMaxLength(MeshNode.SourceMaxLength).IsRequired();
    }
}
