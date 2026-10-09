using Meshtrail.Core.Application.Repositories;
using Meshtrail.Core.Contracts.Teams;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Domain.Teams;

namespace Meshtrail.Core.Application.UseCases.Teams;

internal static class TeamViews
{
    /// <summary>The team as the member <paramref name="userId"/> sees it, with how many gateways can send its chat.</summary>
    public static async Task<TeamDto> ToDtoAsync(
        this Team team, string userId, IMeshGatewayRepository gateways, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var carrying = await gateways.GetCarryingChannelAsync(team.ChannelName, now - Team.ChannelFreshness, cancellationToken);
        return new TeamDto(
            team.Id,
            team.Name,
            team.ChannelName,
            team.JoinCode,
            (team.RoleOf(userId) ?? TeamRole.Member).ToString(),
            [.. team.Members.OrderBy(member => member.JoinedAt).Select(member => new TeamMemberDto(member.UserName, member.Role.ToString(), member.JoinedAt))],
            carrying.Count(gateway => gateway is { CanSend: true } && gateway.Transport != GatewayTransport.Tcp),
            team.CreatedAt);
    }
}
