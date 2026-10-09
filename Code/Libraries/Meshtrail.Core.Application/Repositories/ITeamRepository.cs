using Meshtrail.Core.Domain.Teams;

namespace Meshtrail.Core.Application.Repositories;

/// <summary>Storage for <see cref="Team"/> with its members.</summary>
public interface ITeamRepository
{
    Task<Team?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Team?> GetByJoinCodeAsync(string joinCode, CancellationToken cancellationToken);

    /// <summary>The team that chats on this channel (names are unique), if any.</summary>
    Task<Team?> GetByChannelNameAsync(string channelName, CancellationToken cancellationToken);

    /// <summary>The teams the user is in, oldest first.</summary>
    Task<IReadOnlyList<Team>> GetForUserAsync(string userId, CancellationToken cancellationToken);

    Task<bool> ChannelNameTakenAsync(string channelName, CancellationToken cancellationToken);

    /// <summary>User ids of the team's members (e.g. to push team chat to them).</summary>
    Task<IReadOnlyList<string>> GetMemberIdsAsync(Guid teamId, CancellationToken cancellationToken);

    Task AddAsync(Team team, CancellationToken cancellationToken);

    Task UpdateAsync(Team team, CancellationToken cancellationToken);

    Task DeleteAsync(Team team, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
