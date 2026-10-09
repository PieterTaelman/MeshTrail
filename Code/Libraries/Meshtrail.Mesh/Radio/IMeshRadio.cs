using Meshtastic.Protobufs;

namespace Meshtrail.Mesh.Radio;

/// <summary>Connection state of a radio. The gateway worker turns this into Connecting / Online / Offline.</summary>
public enum MeshRadioState
{
    Disconnected,
    Connecting,
    Connected,
}

/// <summary>
/// A link to one Meshtastic node that acts as our gateway to the mesh. Implementations: <see cref="TcpMeshRadio"/>
/// (real node over WiFi) and the test doubles.
/// Usage: ConnectAsync, then read ReadAllAsync until it ends (= connection lost), then DisconnectAsync and retry.
/// </summary>
public interface IMeshRadio : IAsyncDisposable
{
    /// <summary>Human-readable target, e.g. "tcp://192.168.1.20:4403". Safe to log.</summary>
    string Description { get; }

    MeshRadioState State { get; }

    /// <summary>Opens the link and asks the node for its configuration and node database.</summary>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Everything the node sends, in order. Ends (or throws) when the connection is lost.</summary>
    IAsyncEnumerable<FromRadio> ReadAllAsync(CancellationToken cancellationToken);

    Task SendAsync(ToRadio message, CancellationToken cancellationToken);

    Task DisconnectAsync();
}
