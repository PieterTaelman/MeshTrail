using Meshtrail.Core.Contracts.Mesh;
using Meshtrail.Core.Domain.Mesh;

namespace Meshtrail.Core.Application.UseCases.Mesh;

/// <summary>Domain → contract mapping for the Mesh use cases.</summary>
internal static class MeshMappings
{
    /// <summary>Shown instead of the text of a verification message: the code must never leave the server.</summary>
    public const string HiddenVerificationText = "Verification code";

    public static NodeDto ToDto(this MeshNode node, DateTimeOffset now, bool isGateway, bool isRegistered) => new(
        node.NodeNum,
        node.NodeId,
        node.LongName,
        node.ShortName,
        node.HardwareModel,
        node.Role,
        node.PublicKey is not null,
        node.Source,
        node.FirstSeenAt,
        node.LastHeardAt,
        node.IsOnline(now),
        node.Snr,
        node.Rssi,
        node.HopsAway,
        node.BatteryLevel,
        node.IsExternalPower,
        node.Voltage,
        node.LastPosition?.ToDto(),
        isGateway,
        isRegistered);

    public static MessageDto ToDto(this MeshMessage message) => new(
        message.Id,
        message.Direction.ToString(),
        message.Kind.ToString(),
        message.ChannelIndex,
        message.ChannelName,
        message.GatewayNodeNum,
        message.FromNodeNum,
        message.FromNodeNum is { } from ? MeshNode.FormatNodeId(from) : null,
        message.ToNodeNum,
        message.ToNodeNum is { } to ? MeshNode.FormatNodeId(to) : null,
        message.PeerNodeNum,
        message.Kind == MessageKind.Verification ? HiddenVerificationText : message.Text,
        message.Status.ToString(),
        message.FailureReason,
        message.Snr,
        message.Rssi,
        message.HopsAway,
        message.CreatedAt,
        message.CreatedBy,
        message.SentAt,
        message.AckedAt,
        message.TeamId);

    public static RegistrationDto ToDto(this NodeRegistration registration, MessageStatus? verificationMessageStatus) => new(
        registration.Id,
        registration.NodeNum,
        MeshNode.FormatNodeId(registration.NodeNum),
        registration.LongName,
        registration.ShortName,
        registration.Status.ToString(),
        registration.UserName,
        registration.ClaimedAt,
        registration.CodeExpiresAt,
        registration.AttemptsLeft,
        registration.VerifiedAt,
        registration.RevokedReason,
        registration.VerificationMessageId,
        verificationMessageStatus?.ToString());

    public static PositionDto ToDto(this GeoPosition position) =>
        new(position.Latitude, position.Longitude, position.Altitude, position.Time, position.PrecisionBits);

    public static NodeTracerouteDto ToDto(this NodeTraceroute traceroute, DateTimeOffset now) => new(
        traceroute.Id,
        traceroute.NodeNum,
        traceroute.GetStatus(now).ToString(),
        traceroute.RequestedAt,
        traceroute.RequestedBy,
        traceroute.CompletedAt,
        ToHops(traceroute.RouteTowards, traceroute.SnrTowards),
        ToHops(traceroute.RouteBack, traceroute.SnrBack));

    private static List<RouteHopDto> ToHops(IReadOnlyList<uint> route, IReadOnlyList<double?> snr) =>
        [.. route.Select((nodeNum, index) => new RouteHopDto(nodeNum, MeshNode.FormatNodeId(nodeNum), index < snr.Count ? snr[index] : null))];
}
