namespace Meshtrail.Core.Contracts.Teams;

/// <summary>Role: Owner | Member. User names come from the identity provider.</summary>
public sealed record TeamMemberDto(string UserName, string Role, DateTimeOffset JoinedAt);

/// <summary>
/// A team you are in. ChannelName = the Meshtastic channel the team chats on. GatewaysOnline = online gateways that
/// carried that channel in the last 24 hours (0 = team messages cannot go out).
/// </summary>
public sealed record TeamDto(
    Guid Id,
    string Name,
    string ChannelName,
    string JoinCode,
    string MyRole,
    IReadOnlyList<TeamMemberDto> Members,
    int GatewaysOnline,
    DateTimeOffset CreatedAt);

/// <summary>Body of POST teams. ChannelName: 1–11 characters, not a public preset like LongFast.</summary>
public sealed record CreateTeamRequest(string Name, string ChannelName);

/// <summary>Body of POST teams/join.</summary>
public sealed record JoinTeamRequest(string Code);
