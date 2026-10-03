using Meshtrail.AppHost;

// Local development only: one F5 starts SQL Server, MailPit, the API and the Angular app.
var builder = DistributedApplication.CreateBuilder(args);

// Set once with: dotnet user-secrets set "Parameters:sql-password" "<strong password>" --project Code/Server/Meshtrail.AppHost
var sqlPassword = builder.AddParameter("sql-password", secret: true);

// Fixed port 14330 so SSMS can always connect to 127.0.0.1,14330. Persistent = the container survives AppHost restarts.
var sql = builder.AddSqlServer("sql", password: sqlPassword, port: 14330)
    .WithDataVolume("meshtrail-sql-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDbx();

var database = sql.AddDatabase("MeshtrailDatabase")
    .WithSchemaFromSqlProject(builder.AppHostDirectory);

// Catches every outgoing mail locally so nothing reaches real inboxes.
var mailpit = builder.AddMailPit("mailpit")
    .WithImageTag("v1.31.4")
    .WithSendTestMailCommand();

// Ports come from the WebApi launchSettings.json "https" profile (7301 / 5301).
var api = builder.AddProject<Projects.Meshtrail_WebApi>("api", launchProfileName: "https")
    .WithReference(database)
    .WaitFor(database)
    .WithReference(mailpit)
    .WaitFor(mailpit)
    .WithUrlForEndpoint("https", _ => new ResourceUrlAnnotation { Url = "/scalar", DisplayText = "Scalar (API docs)" })
    .WithHttpHealthCheck("/health/ready", endpointName: "https");

// Fixed port 3000 and not proxied, so the browser origin is exactly http://localhost:3000 (what CORS allows).
builder.AddJavaScriptApp("client-web", "../../../Client-Web", runScriptName: "start")
    .WithNpm(install: true)
    .WithHttpEndpoint(port: 3000, env: "PORT", isProxied: false)
    .WithHttpHealthCheck("/")
    .WithReference(api)
    .WaitForStart(api);

builder.Build().Run();
