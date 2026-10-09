namespace Meshtrail.Core.Infrastructure.Persistence.Entities;

/// <summary>EF row for dbo.GatewayChannels.</summary>
internal sealed class DbGatewayChannel
{
    public Guid GatewayId { get; set; }

    public string ChannelName { get; set; } = string.Empty;

    public DateTimeOffset FirstSeenAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
}
