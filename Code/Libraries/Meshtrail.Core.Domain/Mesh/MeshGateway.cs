using System.Security.Cryptography;
using System.Text;
using Meshtrail.Core.Domain.Common;

namespace Meshtrail.Core.Domain.Mesh;

/// <summary>How a gateway talks to Meshtrail.</summary>
public enum GatewayTransport
{
    /// <summary>The node's MQTT module connects to our broker (the normal case, anywhere in the world).</summary>
    Mqtt,

    /// <summary>A local base station the server reaches over WiFi (TCP API).</summary>
    Tcp,

    /// <summary>A fake gateway of the simulator (development and tests).</summary>
    Simulated,
}

public enum GatewayStatus
{
    /// <summary>Credentials were handed out; waiting for the first uplink, which tells us which node it is.</summary>
    Pending,

    /// <summary>Connected: packets flow both ways.</summary>
    Online,

    /// <summary>Not connected; LastError may say why.</summary>
    Offline,

    /// <summary>Removed by its owner; its login no longer works.</summary>
    Revoked,
}

/// <summary>What happened when an uplink tried to tie the gateway to a node.</summary>
public enum GatewayBindResult
{
    /// <summary>First uplink: the gateway is now tied to this node.</summary>
    Bound,

    /// <summary>Already tied to this node; nothing changed.</summary>
    AlreadyBound,

    /// <summary>The login belongs to another node: the uplink must be ignored.</summary>
    OtherNode,

    /// <summary>The gateway was revoked: the uplink must be ignored.</summary>
    Revoked,
}

/// <summary>
/// A node that connects the mesh around it to Meshtrail. Gateways are ordinary nodes: anyone can turn their node into
/// one by asking for MQTT credentials ("Add gateway") and putting them in the node's MQTT settings. The first uplink
/// with those credentials ties the gateway to that node (proof that the user controls it).
/// TCP and simulated gateways come from the server configuration and have no owner.
/// </summary>
public sealed class MeshGateway
{
    public const int OwnerMaxLength = 256;
    public const int MqttUserNameMaxLength = 50;
    public const int BrokerMaxLength = 50;
    public const int MqttRootMaxLength = 100;
    public const int ChannelMaxLength = 30;
    public const int LastErrorMaxLength = 500;
    public const int FirmwareVersionMaxLength = 50;
    public const int CredentialHashLength = 32;

    /// <summary>One user may run this many gateways (pending ones included); keeps one account from flooding the platform.</summary>
    public const int MaxPerOwner = 20;

    private MeshGateway()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The gateway's own node number; null while Pending.</summary>
    public uint? NodeNum { get; private set; }

    public GatewayTransport Transport { get; private set; }

    /// <summary>Who added the gateway; null for TCP/simulated gateways and unregistered development gateways.</summary>
    public string? OwnerUserId { get; private set; }

    public string? OwnerName { get; private set; }

    /// <summary>MQTT login (not secret). The password is only stored as a hash.</summary>
    public string? MqttUserName { get; private set; }

    public byte[]? CredentialHash { get; private set; }

    /// <summary>The broker (region) the gateway connects to, e.g. "local". Downlinks go out through that broker.</summary>
    public string? Broker { get; private set; }

    /// <summary>The MQTT root topic the gateway uses (e.g. "msh/EU_868"), learned from its uplinks.</summary>
    public string? MqttRoot { get; private set; }

    /// <summary>Channel we send direct messages on: the first channel the gateway uplinked (normally its primary channel).</summary>
    public string? DownlinkChannel { get; private set; }

    public GatewayStatus Status { get; private set; }

    public DateTimeOffset StatusChangedAt { get; private set; }

    public DateTimeOffset? LastUplinkAt { get; private set; }

    public string? LastError { get; private set; }

    public string? FirmwareVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => Status != GatewayStatus.Revoked;

    /// <summary>Rule: we only route through a gateway that is connected and known (bound to a node).</summary>
    public bool CanSend => Status == GatewayStatus.Online && NodeNum is not null;

    /// <summary>A new gateway for <paramref name="ownerUserId"/>; the password is handed out once and only its hash is kept.</summary>
    public static MeshGateway IssueMqtt(string ownerUserId, string ownerName, string broker, string userName, string password, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(userName) || userName.Length > MqttUserNameMaxLength)
        {
            throw new DomainException($"A gateway login must be 1 to {MqttUserNameMaxLength} characters.");
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new DomainException("A gateway needs a password.");
        }

        return new MeshGateway
        {
            Id = Guid.CreateVersion7(now),
            Transport = GatewayTransport.Mqtt,
            OwnerUserId = Cut(ownerUserId, OwnerMaxLength),
            OwnerName = Cut(ownerName, OwnerMaxLength),
            MqttUserName = userName,
            CredentialHash = HashPassword(userName, password),
            Broker = Cut(broker, BrokerMaxLength),
            Status = GatewayStatus.Pending,
            StatusChangedAt = now,
            CreatedAt = now,
        };
    }

    /// <summary>A TCP or simulated gateway from the server configuration: known node, no owner, online.</summary>
    public static MeshGateway Local(GatewayTransport transport, uint nodeNum, DateTimeOffset now)
    {
        if (transport == GatewayTransport.Mqtt)
        {
            throw new DomainException("MQTT gateways are added by their owner.");
        }

        return new MeshGateway
        {
            Id = Guid.CreateVersion7(now),
            NodeNum = nodeNum,
            Transport = transport,
            Status = GatewayStatus.Online,
            StatusChangedAt = now,
            CreatedAt = now,
        };
    }

    /// <summary>
    /// Development only: a gateway that logged in to a broker accepting any login. It has no owner and no credentials,
    /// so its uplinks are accepted but nobody can manage it.
    /// </summary>
    public static MeshGateway Unregistered(string userName, uint nodeNum, string? broker, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        NodeNum = nodeNum,
        Transport = GatewayTransport.Mqtt,
        MqttUserName = Cut(userName, MqttUserNameMaxLength),
        Broker = Cut(broker, BrokerMaxLength),
        Status = GatewayStatus.Online,
        StatusChangedAt = now,
        CreatedAt = now,
    };

    /// <summary>Rebuilds a gateway from stored values. Only the persistence layer should call this.</summary>
    public static MeshGateway Rehydrate(
        Guid id,
        uint? nodeNum,
        GatewayTransport transport,
        string? ownerUserId,
        string? ownerName,
        string? mqttUserName,
        byte[]? credentialHash,
        string? broker,
        string? mqttRoot,
        string? downlinkChannel,
        GatewayStatus status,
        DateTimeOffset statusChangedAt,
        DateTimeOffset? lastUplinkAt,
        string? lastError,
        string? firmwareVersion,
        DateTimeOffset createdAt,
        DateTimeOffset? revokedAt) => new()
    {
        Id = id,
        NodeNum = nodeNum,
        Transport = transport,
        OwnerUserId = ownerUserId,
        OwnerName = ownerName,
        MqttUserName = mqttUserName,
        CredentialHash = credentialHash,
        Broker = broker,
        MqttRoot = mqttRoot,
        DownlinkChannel = downlinkChannel,
        Status = status,
        StatusChangedAt = statusChangedAt,
        LastUplinkAt = lastUplinkAt,
        LastError = lastError,
        FirmwareVersion = firmwareVersion,
        CreatedAt = createdAt,
        RevokedAt = revokedAt,
    };

    /// <summary>
    /// SHA-256 of "login:password". A plain hash is enough here: the password is long and random (not chosen by a
    /// person), so it cannot be guessed from a dictionary.
    /// </summary>
    public static byte[] HashPassword(string userName, string password) => SHA256.HashData(Encoding.UTF8.GetBytes($"{userName}:{password}"));

    public bool IsOwnedBy(string userId) => OwnerUserId is not null && OwnerUserId == userId;

    /// <summary>Does this password belong to this (active) gateway? Compared in constant time.</summary>
    public bool PasswordMatches(string? password) =>
        IsActive
        && CredentialHash is not null
        && MqttUserName is not null
        && password is not null
        && CryptographicOperations.FixedTimeEquals(HashPassword(MqttUserName, password), CredentialHash);

    /// <summary>Ties the gateway to the node that sent the uplink. A login can never move to another node.</summary>
    public GatewayBindResult Bind(uint nodeNum, DateTimeOffset now)
    {
        if (!IsActive)
        {
            return GatewayBindResult.Revoked;
        }

        if (NodeNum is { } bound)
        {
            return bound == nodeNum ? GatewayBindResult.AlreadyBound : GatewayBindResult.OtherNode;
        }

        NodeNum = nodeNum;
        SetStatus(GatewayStatus.Online, now);
        LastError = null;
        return GatewayBindResult.Bound;
    }

    /// <summary>
    /// Something arrived through the gateway: it is online. Remembers the root topic and the first channel (used for
    /// direct messages). Returns true when something worth telling the clients changed (status, root or channel).
    /// </summary>
    public bool RecordUplink(string? mqttRoot, string? channel, DateTimeOffset now)
    {
        if (!IsActive || NodeNum is null)
        {
            return false;
        }

        var changed = SetStatus(GatewayStatus.Online, now);
        LastUplinkAt = now;

        var root = Cut(mqttRoot, MqttRootMaxLength);
        if (root is not null && root != MqttRoot)
        {
            MqttRoot = root;
            changed = true;
        }

        var cleanChannel = UntrustedText.Clean(channel, ChannelMaxLength);
        if (DownlinkChannel is null && cleanChannel is not null)
        {
            DownlinkChannel = cleanChannel;
            changed = true;
        }

        if (changed)
        {
            LastError = null;
        }

        return changed;
    }

    /// <summary>The broker (MQTT) or the connection (TCP/simulator) says whether the gateway is connected. Returns true when the status changed.</summary>
    public bool ChangeConnection(bool connected, string? error, DateTimeOffset now)
    {
        // A pending gateway becomes online only through its first uplink (that is when we learn which node it is).
        if (!IsActive || NodeNum is null)
        {
            return false;
        }

        var newError = connected ? null : Cut(error, LastErrorMaxLength) ?? LastError;
        var changed = SetStatus(connected ? GatewayStatus.Online : GatewayStatus.Offline, now) || newError != LastError;
        LastError = newError;
        return changed;
    }

    /// <summary>Stores what the node told us about itself. Returns false when nothing changed.</summary>
    public bool Identify(string? firmwareVersion)
    {
        var newFirmware = UntrustedText.Clean(firmwareVersion, FirmwareVersionMaxLength) ?? FirmwareVersion;
        if (newFirmware == FirmwareVersion)
        {
            return false;
        }

        FirmwareVersion = newFirmware;
        return true;
    }

    /// <summary>Marks a problem without changing the status (e.g. "this node is already a gateway of someone else").</summary>
    public void ReportError(string error) => LastError = Cut(error, LastErrorMaxLength);

    /// <summary>The owner removes the gateway. Its login stops working; the row stays for history.</summary>
    public void Revoke(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        SetStatus(GatewayStatus.Revoked, now);
        RevokedAt = now;
    }

    private bool SetStatus(GatewayStatus status, DateTimeOffset now)
    {
        if (status == Status)
        {
            return false;
        }

        Status = status;
        StatusChangedAt = now;
        return true;
    }

    private static string? Cut(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length > maxLength ? value[..maxLength] : value;
}
