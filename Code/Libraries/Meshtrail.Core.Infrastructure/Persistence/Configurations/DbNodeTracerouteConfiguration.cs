using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meshtrail.Core.Infrastructure.Persistence.Configurations;

/// <summary>Maps DbNodeTraceroute to dbo.NodeTraceroutes (NodeTraceroutes.sql).</summary>
internal sealed class DbNodeTracerouteConfiguration : IEntityTypeConfiguration<DbNodeTraceroute>
{
    public void Configure(EntityTypeBuilder<DbNodeTraceroute> builder)
    {
        builder.ToTable("NodeTraceroutes", "dbo");
        builder.HasKey(traceroute => traceroute.Id).IsClustered(false);

        builder.Property(traceroute => traceroute.Id).ValueGeneratedNever();
        builder.Property(traceroute => traceroute.Status).HasMaxLength(20).IsRequired();
        builder.Property(traceroute => traceroute.RequestedBy).HasMaxLength(NodeTraceroute.RequestedByMaxLength).IsRequired();
        builder.Property(traceroute => traceroute.RouteTowards).HasMaxLength(400);
        builder.Property(traceroute => traceroute.SnrTowards).HasMaxLength(400);
        builder.Property(traceroute => traceroute.RouteBack).HasMaxLength(400);
        builder.Property(traceroute => traceroute.SnrBack).HasMaxLength(400);
    }
}
