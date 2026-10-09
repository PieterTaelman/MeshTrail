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
        builder.HasKey(gateway => gateway.Id).IsClustered(false);

        builder.Property(gateway => gateway.Id).ValueGeneratedNever();
        builder.Property(gateway => gateway.Transport).HasMaxLength(20).IsRequired();
        builder.Property(gateway => gateway.OwnerUserId).HasMaxLength(MeshGateway.OwnerMaxLength);
        builder.Property(gateway => gateway.OwnerName).HasMaxLength(MeshGateway.OwnerMaxLength);
        builder.Property(gateway => gateway.MqttUserName).HasMaxLength(MeshGateway.MqttUserNameMaxLength);
        builder.Property(gateway => gateway.CredentialHash).HasMaxLength(MeshGateway.CredentialHashLength);
        builder.Property(gateway => gateway.Broker).HasMaxLength(MeshGateway.BrokerMaxLength);
        builder.Property(gateway => gateway.MqttRoot).HasMaxLength(MeshGateway.MqttRootMaxLength);
        builder.Property(gateway => gateway.DownlinkChannel).HasMaxLength(MeshGateway.ChannelMaxLength);
        builder.Property(gateway => gateway.Status).HasMaxLength(20).IsRequired();
        builder.Property(gateway => gateway.LastError).HasMaxLength(MeshGateway.LastErrorMaxLength);
        builder.Property(gateway => gateway.FirmwareVersion).HasMaxLength(MeshGateway.FirmwareVersionMaxLength);
    }
}
