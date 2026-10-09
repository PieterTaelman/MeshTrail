using Meshtrail.Core.Domain.Teams;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meshtrail.Core.Infrastructure.Persistence.Configurations;

/// <summary>Maps DbTeam to dbo.Teams (Teams.sql).</summary>
internal sealed class DbTeamConfiguration : IEntityTypeConfiguration<DbTeam>
{
    public void Configure(EntityTypeBuilder<DbTeam> builder)
    {
        builder.ToTable("Teams", "dbo");
        builder.HasKey(team => team.Id).IsClustered(false);
        builder.Property(team => team.Id).ValueGeneratedNever();
        builder.Property(team => team.Name).HasMaxLength(Team.NameMaxLength).IsRequired();
        builder.Property(team => team.ChannelName).HasMaxLength(Team.ChannelNameMaxLength).IsRequired();
        builder.Property(team => team.JoinCode).HasMaxLength(Team.JoinCodeLength).IsRequired();
        builder.Property(team => team.CreatedBy).HasMaxLength(Team.UserMaxLength).IsRequired();
    }
}

/// <summary>Maps DbTeamMember to dbo.TeamMembers (TeamMembers.sql).</summary>
internal sealed class DbTeamMemberConfiguration : IEntityTypeConfiguration<DbTeamMember>
{
    public void Configure(EntityTypeBuilder<DbTeamMember> builder)
    {
        builder.ToTable("TeamMembers", "dbo");
        builder.HasKey(member => new { member.TeamId, member.UserId });
        builder.Property(member => member.UserId).HasMaxLength(Team.UserMaxLength).IsRequired();
        builder.Property(member => member.UserName).HasMaxLength(Team.UserMaxLength).IsRequired();
        builder.Property(member => member.Role).HasMaxLength(20).IsRequired();

        // Declared so EF inserts a new team before its members, and deletes members with the team.
        builder.HasOne<DbTeam>().WithMany().HasForeignKey(member => member.TeamId).OnDelete(DeleteBehavior.Cascade);
    }
}
