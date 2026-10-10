using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meshtrail.Core.Infrastructure.Persistence.Configurations;

/// <summary>Maps DbNodeRegistration to dbo.NodeRegistrations (NodeRegistrations.sql).</summary>
internal sealed class DbNodeRegistrationConfiguration : IEntityTypeConfiguration<DbNodeRegistration>
{
    public void Configure(EntityTypeBuilder<DbNodeRegistration> builder)
    {
        builder.ToTable("NodeRegistrations", "dbo");
        builder.HasKey(registration => registration.Id).IsClustered(false);

        builder.Property(registration => registration.Id).ValueGeneratedNever();
        builder.Property(registration => registration.UserId).HasMaxLength(NodeRegistration.UserMaxLength).IsRequired();
        builder.Property(registration => registration.UserName).HasMaxLength(NodeRegistration.UserMaxLength).IsRequired();
        builder.Property(registration => registration.Status).HasMaxLength(20).IsRequired();
        builder.Property(registration => registration.PublicKey).HasMaxLength(MeshNode.PublicKeyLength);
        builder.Property(registration => registration.LongName).HasMaxLength(MeshNode.LongNameMaxLength).IsRequired();
        builder.Property(registration => registration.ShortName).HasMaxLength(MeshNode.ShortNameMaxLength).IsRequired();
        builder.Property(registration => registration.CodeHash).HasMaxLength(32);
        builder.Property(registration => registration.RevokedReason).HasMaxLength(NodeRegistration.RevokedReasonMaxLength);
        builder.Property(registration => registration.RowVersion).IsRowVersion();
    }
}
