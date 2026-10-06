using Meshtrail.Mesh.Mqtt;
using Meshtrail.MqttBroker;
using Microsoft.Extensions.Options;

// The MQTT broker for Meshtastic gateways. Gateways log in with their own credentials (the gateway registry comes
// later; in Development any gateway login is accepted), the Meshtrail API logs in with the service account.
var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks and the Aspire dashboard integration, same as every Meshtrail service.
builder.AddServiceDefaults();

builder.Services.Configure<MeshtasticMqttBrokerOptions>(builder.Configuration.GetSection(MeshtasticMqttBrokerOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(provider => new MeshtasticMqttBroker(
    provider.GetRequiredService<IOptions<MeshtasticMqttBrokerOptions>>().Value,
    // No gateway credentials exist yet; outside Development only the service account can log in.
    (_, _, _) => false,
    provider.GetRequiredService<TimeProvider>(),
    provider.GetRequiredService<ILogger<MeshtasticMqttBroker>>()));
builder.Services.AddHostedService<BrokerHost>();
builder.Services.AddHealthChecks().AddCheck<BrokerHealthCheck>(BrokerHealthCheck.Name);

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
