using Meshtrail.Mesh;
using Meshtrail.Mesh.Mqtt;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Meshtrail.MqttBroker;

/// <summary>Starts and stops the broker with the host, and (optionally) logs a summary of every uplink.</summary>
internal sealed partial class BrokerHost(
    MeshtasticMqttBroker broker,
    IOptions<MeshtasticMqttBrokerOptions> options,
    ILogger<BrokerHost> logger) : BackgroundService
{
    public bool IsRunning { get; private set; }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await broker.StartAsync();
        IsRunning = true;
        LogStarted(options.Value.Port, options.Value.BindAddress, options.Value.AllowAnyGateway);
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        IsRunning = false;
        await broker.StopAsync();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var uplink in broker.ReadAllAsync(stoppingToken))
            {
                if (options.Value.LogUplinks)
                {
                    LogUplink(uplink.ClientId, uplink.Topic?.Kind.ToString() ?? "Other", uplink.Topic?.Channel, Describe(uplink));
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private static string Describe(MqttUplink uplink) => uplink.Envelope?.Packet is { } packet
        ? packet.Decoded is { } data
            ? $"{NodeIds.Format(packet.From)} -> {(packet.To == NodeIds.Broadcast ? "all" : NodeIds.Format(packet.To))} {data.Portnum}"
            : $"{NodeIds.Format(packet.From)} encrypted (turn off MQTT encryption on the gateway)"
        : $"{uplink.Payload.Length} bytes";

    [LoggerMessage(Level = LogLevel.Information, Message = "MQTT broker listening on {BindAddress}:{Port} (any gateway login accepted: {AllowAnyGateway})")]
    private partial void LogStarted(int port, string bindAddress, bool allowAnyGateway);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uplink from {ClientId}: {Kind} on {Channel}: {Summary}")]
    private partial void LogUplink(string? clientId, string kind, string? channel, string summary);
}

/// <summary>Healthy while the broker runs; the description says how many gateways are connected.</summary>
internal sealed class BrokerHealthCheck(MeshtasticMqttBroker broker, IEnumerable<IHostedService> hostedServices) : IHealthCheck
{
    public const string Name = "mqtt-broker";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var running = hostedServices.OfType<BrokerHost>().Any(host => host.IsRunning);
        return Task.FromResult(running
            ? HealthCheckResult.Healthy($"{broker.ConnectedGateways.Count} gateway(s) connected.")
            : HealthCheckResult.Unhealthy("The MQTT broker is not running."));
    }
}
