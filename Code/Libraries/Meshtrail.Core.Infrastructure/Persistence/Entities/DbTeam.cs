namespace Meshtrail.Core.Infrastructure.Persistence.Entities;

/// <summary>EF row for dbo.Teams.</summary>
internal sealed class DbTeam
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ChannelName { get; set; } = string.Empty;

    public string JoinCode { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>EF row for dbo.TeamMembers.</summary>
internal sealed class DbTeamMember
{
    public Guid TeamId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public DateTimeOffset JoinedAt { get; set; }
}
