using Meshtrail.Core.Domain.Common;

namespace Meshtrail.Core.Domain.Teams;

public enum TeamRole
{
    /// <summary>Created the team: can renew the join code.</summary>
    Owner,

    Member,
}

/// <summary>A person in a team.</summary>
public sealed record TeamMember(string UserId, string UserName, TeamRole Role, DateTimeOffset JoinedAt);

/// <summary>
/// A group of people who chat on their own Meshtastic channel. The team configures that channel (same name and key)
/// on its nodes and on at least one gateway; Meshtrail only knows the channel name, never its key. People join with
/// the join code. Team chat is visible to members only.
/// </summary>
public sealed class Team
{
    public const int NameMaxLength = 50;

    /// <summary>The firmware allows channel names of at most 11 characters.</summary>
    public const int ChannelNameMaxLength = 11;

    public const int JoinCodeLength = 8;
    public const int UserMaxLength = 256;
    public const int MaxMembers = 200;

    /// <summary>A gateway counts as "carrying" the team's channel when it uplinked on it this recently.</summary>
    public static readonly TimeSpan ChannelFreshness = TimeSpan.FromHours(24);

    /// <summary>
    /// Default channels everyone in a region shares (the preset names). A team on one of them would turn public chat
    /// into "team chat", so they are not allowed.
    /// </summary>
    public static readonly IReadOnlySet<string> PublicChannelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "LongFast", "LongSlow", "LongModerate", "LongTurbo", "VeryLongSlow",
        "MediumSlow", "MediumFast", "ShortSlow", "ShortFast", "ShortTurbo",
    };

    private readonly List<TeamMember> _members = [];

    private Team()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>The Meshtastic channel the team chats on (as the gateways report it in their MQTT topics).</summary>
    public string ChannelName { get; private set; } = string.Empty;

    /// <summary>Shared with people who may join. Not a strong secret: the owner can renew it.</summary>
    public string JoinCode { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public IReadOnlyList<TeamMember> Members => _members;

    public static Team Create(string name, string channelName, string joinCode, string userId, string userName, DateTimeOffset now)
    {
        var cleanName = name?.Trim() ?? string.Empty;
        if (cleanName.Length is 0 or > NameMaxLength)
        {
            throw new DomainException($"A team name must be 1 to {NameMaxLength} characters.");
        }

        var team = new Team
        {
            Id = Guid.CreateVersion7(now),
            Name = cleanName,
            ChannelName = CheckChannelName(channelName),
            JoinCode = joinCode,
            CreatedAt = now,
            CreatedBy = Cut(userName),
        };
        team._members.Add(new TeamMember(Cut(userId), Cut(userName), TeamRole.Owner, now));
        return team;
    }

    public static Team Rehydrate(
        Guid id, string name, string channelName, string joinCode, DateTimeOffset createdAt, string createdBy, IEnumerable<TeamMember> members)
    {
        var team = new Team { Id = id, Name = name, ChannelName = channelName, JoinCode = joinCode, CreatedAt = createdAt, CreatedBy = createdBy };
        team._members.AddRange(members);
        return team;
    }

    /// <summary>Rule: 1–11 characters, no MQTT topic characters, and not a public preset channel.</summary>
    public static string CheckChannelName(string? channelName)
    {
        var name = channelName?.Trim() ?? string.Empty;
        if (name.Length is 0 or > ChannelNameMaxLength || name.IndexOfAny(['/', '#', '+', ' ']) >= 0)
        {
            throw new DomainException($"A channel name must be 1 to {ChannelNameMaxLength} characters without spaces, '/', '#' or '+'.");
        }

        if (PublicChannelNames.Contains(name))
        {
            throw new DomainException($"\"{name}\" is a public channel everyone shares. Give the team its own channel name.");
        }

        return name;
    }

    public bool IsMember(string userId) => _members.Any(member => member.UserId == userId);

    public TeamRole? RoleOf(string userId) => _members.FirstOrDefault(member => member.UserId == userId)?.Role;

    /// <summary>Adds the user. Returns false when they already are a member.</summary>
    public bool Join(string userId, string userName, DateTimeOffset now)
    {
        if (IsMember(userId))
        {
            return false;
        }

        if (_members.Count >= MaxMembers)
        {
            throw new DomainException($"A team can have at most {MaxMembers} members.");
        }

        _members.Add(new TeamMember(Cut(userId), Cut(userName), TeamRole.Member, now));
        return true;
    }

    /// <summary>
    /// Removes the user. The last owner may only leave as the last member (the team then ends); otherwise someone
    /// would be left without anybody who can renew the code. Returns true when the team is now empty.
    /// </summary>
    public bool Leave(string userId)
    {
        var member = _members.FirstOrDefault(item => item.UserId == userId)
            ?? throw new DomainException("You are not a member of this team.");

        var owners = _members.Count(item => item.Role == TeamRole.Owner);
        if (member.Role == TeamRole.Owner && owners == 1 && _members.Count > 1)
        {
            throw new DomainException("You are the team's only owner. The others must leave first.");
        }

        _members.Remove(member);
        return _members.Count == 0;
    }

    /// <summary>Only an owner can renew the join code (e.g. when it leaked).</summary>
    public void RenewJoinCode(string userId, string joinCode)
    {
        if (RoleOf(userId) != TeamRole.Owner)
        {
            throw new DomainException("Only the team's owner can renew the join code.");
        }

        JoinCode = joinCode;
    }

    private static string Cut(string value) => value.Length > UserMaxLength ? value[..UserMaxLength] : value;
}
