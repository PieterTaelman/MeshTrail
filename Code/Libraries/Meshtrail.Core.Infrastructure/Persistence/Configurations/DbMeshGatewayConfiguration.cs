using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meshtrail.Core.Infrastructure.Persistence.Configurations;

/// <summary>Maps DbMeshGateway to dbo.MeshGateways (MeshGateways.sql).</summary>
internal sealed class DbMeshGatewayConfiguration : IEntityTypeConfiguration<DbMeshGateway>
{
    public void Configure(EntityTypeBuilder<DbMeshGateway> builder)
    {
        builder.ToTable("MeshGateways", "dbo");
        builder.HasKey(gateway => gateway.GatewayKey);

        builder.Property(gateway => gateway.GatewayKey).HasMaxLength(MeshGateway.KeyMaxLength).ValueGeneratedNever();
        builder.Property(gateway => gateway.Mode).HasMaxLength(MeshGateway.ModeMaxLength).IsRequired();
        builder.Property(gateway => gateway.Status).HasMaxLength(20).IsRequired();
        builder.Property(gateway => gateway.LastError).HasMaxLength(MeshGateway.LastErrorMaxLength);
        builder.Property(gateway => gateway.FirmwareVersion).HasMaxLength(MeshGateway.FirmwareVersionMaxLength);
    }
}
