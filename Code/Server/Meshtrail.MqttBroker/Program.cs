using Meshtrail.Mesh.Mqtt;
using Meshtrail.MqttBroker;
using Microsoft.Extensions.Options;

// The MQTT broker for Meshtastic gateways. Gateways log in with the credentials their owner got from Meshtrail
// ("Add gateway"); the broker asks the API whether a login is valid. The Meshtrail API logs in with the service account.
var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry, health checks, service discovery and the Aspire dashboard integration, same as every Meshtrail service.
builder.AddServiceDefaults();

builder.Services.Configure<MeshtasticMqttBrokerOptions>(builder.Configuration.GetSection(MeshtasticMqttBrokerOptions.SectionName));
builder.Services.Configure<ApiAuthOptions>(builder.Configuration.GetSection(ApiAuthOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient(ApiAuthOptions.HttpClientName, (provider, client) =>
    client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<ApiAuthOptions>>().Value.Url));
builder.Services.AddSingleton<ApiGatewayAuthenticator>();
builder.Services.AddSingleton(provider => new MeshtasticMqttBroker(
    provider.GetRequiredService<IOptions<MeshtasticMqttBrokerOptions>>().Value,
    provider.GetRequiredService<ApiGatewayAuthenticator>().AuthenticateAsync,
    provider.GetRequiredService<TimeProvider>(),
    provider.GetRequiredService<ILogger<MeshtasticMqttBroker>>()));
builder.Services.AddHostedService<BrokerHost>();
builder.Services.AddHealthChecks().AddCheck<BrokerHealthCheck>(BrokerHealthCheck.Name);

var app = builder.Build();
app.MapDefaultEndpoints();
app.Run();
