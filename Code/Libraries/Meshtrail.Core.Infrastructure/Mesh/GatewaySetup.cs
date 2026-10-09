using Meshtrail.Core.Application.Abstractions;
using Meshtrail.Core.Contracts.Mesh;
using Microsoft.Extensions.Options;

namespace Meshtrail.Core.Infrastructure.Mesh;

/// <summary><see cref="IGatewaySetup"/> from Meshtastic:Mqtt.</summary>
public sealed class GatewaySetup(IOptions<MeshMqttOptions> options) : IGatewaySetup
{
    public string Broker => options.Value.Broker;

    public MqttSetupDto Setup => new(
        string.IsNullOrWhiteSpace(options.Value.PublicHost) ? null : options.Value.PublicHost,
        options.Value.PublicPort,
        options.Value.UseTls,
        options.Value.Root);

    public bool AcceptUnregisteredGateways => options.Value.AcceptUnregisteredGateways;
}
