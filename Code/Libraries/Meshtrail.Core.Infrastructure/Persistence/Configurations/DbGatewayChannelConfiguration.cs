using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meshtrail.Core.Infrastructure.Persistence.Configurations;

/// <summary>Maps DbGatewayChannel to dbo.GatewayChannels (GatewayChannels.sql).</summary>
internal sealed class DbGatewayChannelConfiguration : IEntityTypeConfiguration<DbGatewayChannel>
{
    public void Configure(EntityTypeBuilder<DbGatewayChannel> builder)
    {
        builder.ToTable("GatewayChannels", "dbo");
        builder.HasKey(channel => new { channel.GatewayId, channel.ChannelName });
        builder.Property(channel => channel.ChannelName).HasMaxLength(MeshGateway.ChannelMaxLength).IsRequired();

        // Declared so EF inserts a new gateway before its first channel in the same save.
        builder.HasOne<DbMeshGateway>().WithMany().HasForeignKey(channel => channel.GatewayId);
    }
}
