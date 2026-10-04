# Meshtrail

Skeleton solution: .NET 10 Web API (Clean Architecture / DDD / CQRS on martinothamar/Mediator), SQL Server with an
SSDT database project, and an Angular 22 front end — all started locally with one .NET Aspire AppHost.

The business domain is not defined yet. The `Samples` module is the worked example to copy
([Documentation/Samples](Documentation/Samples/README.md)). Conventions for humans and AI assistants: [AGENTS.md](AGENTS.md).

## Getting started

### Prerequisites

| Tool | Version | Why |
|---|---|---|
| .NET SDK | 10.0.400+ | backend, AppHost |
| Node.js | **22.22.3+ or 24.15+** | Angular 22 refuses older versions |
| Docker Desktop | recent | SQL Server and MailPit containers |
| HTTPS dev certificate | — | `dotnet dev-certs https --trust` |
| Visual Studio 2026 + SSDT (optional) | — | building/publishing the `.sqlproj` |

### One-time setup

```bash
dotnet dev-certs https --trust
dotnet user-secrets set "Parameters:sql-password" "<a strong password>" --project Code/Server/Meshtrail.AppHost
```

The password must meet SQL Server's complexity rules (8+ chars, upper, lower, digit, symbol). It is stored in your
user secrets, never in the repo. Because the container and its data volume are persistent, keep using the same password.

### Run everything

```bash
dotnet run --project Code/Server/Meshtrail.AppHost
```

Or press F5 on `Meshtrail.AppHost` in Visual Studio / Rider. The Aspire dashboard opens and starts:

| Resource | URL | Notes |
|---|---|---|
| `sql` | `127.0.0.1,14330` | SQL Server container (persistent) with the Dbx web admin UI |
| `MeshtrailDatabase` | — | Built from the `.sqlproj`; rebuilt only when a table script changes. Seed runs every start |
| `mailpit` | link in dashboard | Catches every outgoing mail. Dashboard command **Send test mail** |
| `api` | https://localhost:7301/scalar | Web API + Scalar API docs, health at `/health/ready` |
| `client-web` | http://localhost:3000 | Angular app (`npm install` runs automatically) |

Dashboard command **Rebuild database** (on `MeshtrailDatabase`) drops all local data and rebuilds from scratch.

### Mesh gateway (Meshtastic)

In Development the API runs a **simulated** mesh (5 fake nodes around Belgium), so no hardware is needed. To use your
real gateway node, put its address in user secrets (never in appsettings, never in git):

```bash
dotnet user-secrets set "Meshtastic:Gateway:Mode" "Tcp" --project Code/Server/Meshtrail.WebApi
dotnet user-secrets set "Meshtastic:Gateway:Host" "<node-ip>" --project Code/Server/Meshtrail.WebApi
```

| Key | Default | Meaning |
|---|---|---|
| `Meshtastic:Gateway:Mode` | `Tcp` (`Simulated` in Development) | Real node over WiFi or the fake mesh |
| `Meshtastic:Gateway:Host` / `Port` | — / `4403` | Address of the gateway node |
| `Meshtastic:Outbound:MinInterval` | `00:00:10` | Minimum pause between packets we send (EU868 duty cycle) |
| `Meshtastic:Outbound:AckTimeout` | `00:01:30` | Sent messages without a delivery report after this long become Failed |
| `Meshtastic:Retention:PositionDays` | `30` | Position history kept this many days |
| `Jobs:NodePositionRetention` | enabled, `15 3 * * *` | Schedule of the history clean-up |

The node accepts one TCP client only: close the phone app's WiFi connection first. In the simulator, the contact
links of the fake nodes and the verification codes they receive are written to the API log. Details, all keys and how to set up
a node: [Documentation/Mesh/README.md](Documentation/Mesh/README.md).

### Connect with SSMS / Azure Data Studio

- Server: `127.0.0.1,14330` — use the IP, **not** `localhost` (avoids named-pipe/IPv6 lookups that fail against the container)
- Authentication: SQL Server, user `sa`, password = your `Parameters:sql-password` secret
- Trust server certificate: yes

## Everyday commands

```bash
dotnet build Meshtrail.slnx                                       # 0 warnings expected
dotnet test --project Code/Tests/Meshtrail.Core.UnitTests
dotnet test --project Code/Tests/Meshtrail.Core.IntegrationTests  # needs Docker, or MESHTRAIL_TEST_SQL

cd Client-Web
npm ci
npm start          # dev server on :3000 (normally started by the AppHost)
npm run lint
npm run format
npm test           # Vitest
npm run e2e        # Playwright, against a running AppHost
```

Integration tests without Docker:

```bash
MESHTRAIL_TEST_SQL="Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true" \
  dotnet test --project Code/Tests/Meshtrail.Core.IntegrationTests
```

## Deployment

Aspire is for local development only. Production runs on-prem; the deployment pipeline (`.github/workflows/cd.yml`)
is a stub until that is defined. The database is deployed from the `.sqlproj` (dacpac), not by EF.
