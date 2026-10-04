using System.Globalization;
using Meshtrail.Core.Domain.Mesh;
using Meshtrail.Core.Infrastructure.Persistence.Entities;

namespace Meshtrail.Core.Infrastructure.Persistence.Mappers;

/// <summary>Domain ↔ Db conversion for the Mesh tables. Node numbers are uint in the domain and BIGINT in SQL.</summary>
internal static class MeshMapper
{
    // ---- MeshNode

    public static MeshNode ToDomain(this DbMeshNode row) => MeshNode.Rehydrate(
        (uint)row.NodeNum,
        row.LongName,
        row.ShortName,
        row.HardwareModel,
        row.Role,
        row.PublicKey,
        row.Source,
        row.FirstSeenAt,
        row.LastHeardAt,
        row.Snr,
        row.Rssi,
        row.HopsAway,
        row.BatteryLevel,
        row.Voltage,
        row is { Latitude: { } latitude, Longitude: { } longitude, PositionTime: { } time }
            ? GeoPosition.Create(latitude, longitude, row.Altitude, time, row.PositionPrecision ?? 0)
            : null,
        row.UpdatedAt);

    public static DbMeshNode ToDb(this MeshNode node)
    {
        var row = new DbMeshNode { NodeNum = node.NodeNum };
        node.CopyTo(row);
        return row;
    }

    public static void CopyTo(this MeshNode node, DbMeshNode row)
    {
        row.NodeId = node.NodeId;
        row.LongName = node.LongName;
        row.ShortName = node.ShortName;
        row.HardwareModel = node.HardwareModel;
        row.Role = node.Role;
        row.PublicKey = node.PublicKey;
        row.Source = node.Source;
        row.FirstSeenAt = node.FirstSeenAt;
        row.LastHeardAt = node.LastHeardAt;
        row.Snr = node.Snr;
        row.Rssi = node.Rssi;
        row.HopsAway = node.HopsAway;
        row.BatteryLevel = node.BatteryLevel;
        row.Voltage = node.Voltage;
        row.Latitude = node.LastPosition?.Latitude;
        row.Longitude = node.LastPosition?.Longitude;
        row.Altitude = node.LastPosition?.Altitude;
        row.PositionTime = node.LastPosition?.Time;
        row.PositionPrecision = node.LastPosition?.PrecisionBits;
        row.UpdatedAt = node.UpdatedAt;
    }

    // ---- NodePosition

    public static DbNodePosition ToDb(this NodePosition position) => new()
    {
        Id = position.Id,
        NodeNum = position.NodeNum,
        Latitude = position.Position.Latitude,
        Longitude = position.Position.Longitude,
        Altitude = position.Position.Altitude,
        PositionTime = position.Position.Time,
        Precision = position.Position.PrecisionBits,
        ReceivedAt = position.ReceivedAt,
    };

    // ---- MeshGateway

    public static MeshGateway ToDomain(this DbMeshGateway row) => MeshGateway.Rehydrate(
        row.GatewayKey,
        row.Mode,
        Enum.Parse<GatewayStatus>(row.Status),
        row.StatusChangedAt,
        row.LastConnectedAt,
        row.LastError,
        row.NodeNum is { } nodeNum ? (uint)nodeNum : null,
        row.FirmwareVersion);

    public static DbMeshGateway ToDb(this MeshGateway gateway)
    {
        var row = new DbMeshGateway { GatewayKey = gateway.GatewayKey };
        gateway.CopyTo(row);
        return row;
    }

    public static void CopyTo(this MeshGateway gateway, DbMeshGateway row)
    {
        row.Mode = gateway.Mode;
        row.Status = gateway.Status.ToString();
        row.StatusChangedAt = gateway.StatusChangedAt;
        row.LastConnectedAt = gateway.LastConnectedAt;
        row.LastError = gateway.LastError;
        row.NodeNum = gateway.NodeNum;
        row.FirmwareVersion = gateway.FirmwareVersion;
    }

    // ---- NodeTraceroute

    public static NodeTraceroute ToDomain(this DbNodeTraceroute row) => NodeTraceroute.Rehydrate(
        row.Id,
        (uint)row.NodeNum,
        (uint)row.PacketId,
        Enum.Parse<TracerouteStatus>(row.Status),
        row.RequestedAt,
        row.RequestedBy,
        row.CompletedAt,
        ParseRoute(row.RouteTowards),
        ParseSnr(row.SnrTowards),
        ParseRoute(row.RouteBack),
        ParseSnr(row.SnrBack));

    public static DbNodeTraceroute ToDb(this NodeTraceroute traceroute)
    {
        var row = new DbNodeTraceroute { Id = traceroute.Id };
        traceroute.CopyTo(row);
        return row;
    }

    public static void CopyTo(this NodeTraceroute traceroute, DbNodeTraceroute row)
    {
        row.NodeNum = traceroute.NodeNum;
        row.PacketId = traceroute.PacketId;
        row.Status = traceroute.Status.ToString();
        row.RequestedAt = traceroute.RequestedAt;
        row.RequestedBy = traceroute.RequestedBy;
        row.CompletedAt = traceroute.CompletedAt;
        row.RouteTowards = string.Join(',', traceroute.RouteTowards.Select(hop => hop.ToString(CultureInfo.InvariantCulture)));
        row.SnrTowards = string.Join(',', traceroute.SnrTowards.Select(FormatSnr));
        row.RouteBack = string.Join(',', traceroute.RouteBack.Select(hop => hop.ToString(CultureInfo.InvariantCulture)));
        row.SnrBack = string.Join(',', traceroute.SnrBack.Select(FormatSnr));
    }

    // ---- NodeRegistration

    public static NodeRegistration ToDomain(this DbNodeRegistration row) => NodeRegistration.Rehydrate(
        row.Id,
        (uint)row.NodeNum,
        row.UserId,
        row.UserName,
        Enum.Parse<RegistrationStatus>(row.Status),
        row.PublicKey,
        row.LongName,
        row.ShortName,
        row.CodeHash,
        row.CodeExpiresAt,
        row.FailedAttempts,
        row.VerificationMessageId,
        row.ClaimedAt,
        row.VerifiedAt,
        row.RevokedAt,
        row.RevokedReason,
        row.RowVersion);

    public static DbNodeRegistration ToDb(this NodeRegistration registration)
    {
        var row = new DbNodeRegistration { Id = registration.Id, RowVersion = registration.RowVersion };
        registration.CopyTo(row);
        return row;
    }

    public static void CopyTo(this NodeRegistration registration, DbNodeRegistration row)
    {
        row.NodeNum = registration.NodeNum;
        row.UserId = registration.UserId;
        row.UserName = registration.UserName;
        row.Status = registration.Status.ToString();
        row.PublicKey = registration.PublicKey;
        row.LongName = registration.LongName;
        row.ShortName = registration.ShortName;
        row.CodeHash = registration.CodeHash;
        row.CodeExpiresAt = registration.CodeExpiresAt;
        row.FailedAttempts = registration.FailedAttempts;
        row.VerificationMessageId = registration.VerificationMessageId;
        row.ClaimedAt = registration.ClaimedAt;
        row.VerifiedAt = registration.VerifiedAt;
        row.RevokedAt = registration.RevokedAt;
        row.RevokedReason = registration.RevokedReason;
    }

    // ---- MeshMessage

    public static MeshMessage ToDomain(this DbMeshMessage row) => MeshMessage.Rehydrate(
        row.Id,
        Enum.Parse<MessageDirection>(row.Direction),
        Enum.Parse<MessageKind>(row.Kind),
        row.ChannelIndex,
        row.FromNodeNum is { } from ? (uint)from : null,
        row.ToNodeNum is { } to ? (uint)to : null,
        row.Text,
        (uint)row.PacketId,
        Enum.Parse<MessageStatus>(row.Status),
        row.FailureReason,
        row.Snr,
        row.Rssi,
        row.HopsAway,
        row.CreatedAt,
        row.CreatedBy,
        row.SentAt,
        row.AckedAt);

    public static DbMeshMessage ToDb(this MeshMessage message)
    {
        var row = new DbMeshMessage { Id = message.Id };
        message.CopyTo(row);
        return row;
    }

    public static void CopyTo(this MeshMessage message, DbMeshMessage row)
    {
        row.Direction = message.Direction.ToString();
        row.Kind = message.Kind.ToString();
        row.ChannelIndex = message.ChannelIndex;
        row.FromNodeNum = message.FromNodeNum;
        row.ToNodeNum = message.ToNodeNum;
        row.Text = message.Text;
        row.PacketId = message.PacketId;
        row.Status = message.Status.ToString();
        row.FailureReason = message.FailureReason;
        row.Snr = message.Snr;
        row.Rssi = message.Rssi;
        row.HopsAway = message.HopsAway;
        row.CreatedAt = message.CreatedAt;
        row.CreatedBy = message.CreatedBy;
        row.SentAt = message.SentAt;
        row.AckedAt = message.AckedAt;
    }

    private static string FormatSnr(double? snr) => snr?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static List<uint> ParseRoute(string? value) =>
        string.IsNullOrEmpty(value) ? [] : [.. value.Split(',').Select(hop => uint.Parse(hop, CultureInfo.InvariantCulture))];

    private static List<double?> ParseSnr(string? value) =>
        string.IsNullOrEmpty(value)
            ? []
            : [.. value.Split(',').Select(snr => snr.Length == 0 ? (double?)null : double.Parse(snr, CultureInfo.InvariantCulture))];
}
