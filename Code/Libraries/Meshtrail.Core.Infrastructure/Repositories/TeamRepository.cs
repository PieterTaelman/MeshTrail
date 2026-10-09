using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Domain.Teams;
using Meshtrail.Core.Infrastructure.Persistence;
using Meshtrail.Core.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Meshtrail.Core.Infrastructure.Repositories;

/// <summary>Teams with their members. Member rows are kept in sync with the team on UpdateAsync.</summary>
internal sealed class TeamRepository(MeshtrailDbContext dbContext) : ITeamRepository
{
    private readonly Dictionary<Team, DbTeam> _rows = new(ReferenceEqualityComparer.Instance);

    private DbSet<DbTeam> Teams => dbContext.Set<DbTeam>();

    private DbSet<DbTeamMember> Members => dbContext.Set<DbTeamMember>();

    public async Task<Team?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await LoadAsync(await Teams.FirstOrDefaultAsync(row => row.Id == id, cancellationToken), cancellationToken);

    public async Task<Team?> GetByJoinCodeAsync(string joinCode, CancellationToken cancellationToken) =>
        await LoadAsync(await Teams.FirstOrDefaultAsync(row => row.JoinCode == joinCode, cancellationToken), cancellationToken);

    public async Task<Team?> GetByChannelNameAsync(string channelName, CancellationToken cancellationToken) =>
        await LoadAsync(await Teams.FirstOrDefaultAsync(row => row.ChannelName == channelName, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<Team>> GetForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var teamIds = Members.Where(member => member.UserId == userId).Select(member => member.TeamId);
        var rows = await Teams.Where(team => teamIds.Contains(team.Id)).OrderBy(team => team.CreatedAt).ToListAsync(cancellationToken);
        var result = new List<Team>(rows.Count);
        foreach (var row in rows)
        {
            result.Add((await LoadAsync(row, cancellationToken))!);
        }

        return result;
    }

    public async Task<bool> ChannelNameTakenAsync(string channelName, CancellationToken cancellationToken) =>
        await Teams.AnyAsync(team => team.ChannelName == channelName, cancellationToken);

    public async Task<IReadOnlyList<string>> GetMemberIdsAsync(Guid teamId, CancellationToken cancellationToken) =>
        await Members.AsNoTracking().Where(member => member.TeamId == teamId).Select(member => member.UserId).ToListAsync(cancellationToken);

    public Task AddAsync(Team team, CancellationToken cancellationToken)
    {
        var row = new DbTeam { Id = team.Id };
        CopyTo(team, row);
        Teams.Add(row);
        _rows[team] = row;
        Members.AddRange(team.Members.Select(member => ToRow(team.Id, member)));
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(Team team, CancellationToken cancellationToken)
    {
        if (!_rows.TryGetValue(team, out var row))
        {
            row = await Teams.FirstAsync(candidate => candidate.Id == team.Id, cancellationToken);
            _rows[team] = row;
        }

        CopyTo(team, row);

        // Add, update and remove member rows so they match the team.
        var stored = await Members.Where(member => member.TeamId == team.Id).ToListAsync(cancellationToken);
        foreach (var member in team.Members)
        {
            var existing = stored.FirstOrDefault(candidate => candidate.UserId == member.UserId);
            if (existing is null)
            {
                Members.Add(ToRow(team.Id, member));
            }
            else
            {
                existing.UserName = member.UserName;
                existing.Role = member.Role.ToString();
            }
        }

        Members.RemoveRange(stored.Where(candidate => team.Members.All(member => member.UserId != candidate.UserId)));
    }

    public async Task DeleteAsync(Team team, CancellationToken cancellationToken)
    {
        var row = _rows.TryGetValue(team, out var tracked) ? tracked : await Teams.FirstAsync(candidate => candidate.Id == team.Id, cancellationToken);
        Members.RemoveRange(await Members.Where(member => member.TeamId == team.Id).ToListAsync(cancellationToken));
        Teams.Remove(row);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await dbContext.SaveChangesAsync(cancellationToken);

    private async Task<Team?> LoadAsync(DbTeam? row, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return null;
        }

        var members = await Members.AsNoTracking().Where(member => member.TeamId == row.Id).ToListAsync(cancellationToken);
        var team = Team.Rehydrate(
            row.Id,
            row.Name,
            row.ChannelName,
            row.JoinCode,
            row.CreatedAt,
            row.CreatedBy,
            members.Select(member => new TeamMember(member.UserId, member.UserName, Enum.Parse<TeamRole>(member.Role), member.JoinedAt)));
        _rows[team] = row;
        return team;
    }

    private static void CopyTo(Team team, DbTeam row)
    {
        row.Name = team.Name;
        row.ChannelName = team.ChannelName;
        row.JoinCode = team.JoinCode;
        row.CreatedAt = team.CreatedAt;
        row.CreatedBy = team.CreatedBy;
    }

    private static DbTeamMember ToRow(Guid teamId, TeamMember member) => new()
    {
        TeamId = teamId,
        UserId = member.UserId,
        UserName = member.UserName,
        Role = member.Role.ToString(),
        JoinedAt = member.JoinedAt,
    };
}
