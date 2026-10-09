using Meshtrail.Core.Contracts.Mesh;

namespace Meshtrail.Core.Application.Abstractions;

/// <summary>A new gateway login. The password is random and long; it is shown to the user once.</summary>
public sealed record GatewayCredentials(string UserName, string Password);

/// <summary>Creates gateway logins. An interface so tests can use known values.</summary>
public interface IGatewayCredentialGenerator
{
    GatewayCredentials NewCredentials();
}

/// <summary>Server settings handlers need for MQTT gateways (from Meshtastic:Mqtt). Implemented in Infrastructure.</summary>
public interface IGatewaySetup
{
    /// <summary>Name of the broker (region) new gateways connect to.</summary>
    string Broker { get; }

    /// <summary>What to put in the node's MQTT settings.</summary>
    MqttSetupDto Setup { get; }

    /// <summary>Development only: accept uplinks from logins that are not registered (they become ownerless gateways).</summary>
    bool AcceptUnregisteredGateways { get; }
}
