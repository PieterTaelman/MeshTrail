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
| `mqtt-broker` | `<this PC>:1883` (MQTT) | Broker for Meshtastic gateways (MQTT module of the node); health on http://localhost:5311 |
| `client-web` | http://localhost:3000 | Angular app (`npm install` runs automatically) |

Dashboard command **Rebuild database** (on `MeshtrailDatabase`) drops all local data and rebuilds from scratch.

### Accounts

Open http://localhost:3000 → **Register** (email, first name, last name, password). The confirmation mail lands in
**MailPit** (link in the dashboard); its link confirms the address, then sign in. Without the AppHost/Docker the mail,
link included, is written to the API log. The map is public; chat, nodes, gateways and teams need an account.

| Key | Default | Meaning |
|---|---|---|
| `Authentication:Mode` | `Local` | Our own accounts and tokens (`Development` is for tests) |
| `Authentication:Local:SigningKey` | — (dev key in Development) | Token signing key, at least 32 characters. Secret |
| `Authentication:Local:ClientBaseUrl` | `http://localhost:3000` | Where links in mails point to |
| `Email:From` / `SmtpHost` / `SmtpPort` | — | SMTP server; MailPit is used automatically under the AppHost |

More: [Documentation/Accounts/README.md](Documentation/Accounts/README.md).

### Mesh gateways (Meshtastic)

In Development the API runs a **simulated** mesh (3 fake gateways and 5 hikers around Belgium) and logs in to the
local MQTT broker, so no hardware is needed. To connect a real node as a gateway: **Profile → My gateways**, choose
the node (or type its `!id`), then enter the shown login, password and your PC's LAN IP in the node's MQTT settings
(encryption off, uplink/downlink on for the primary channel). The login only works for that node. Group chat happens
in **My teams**, each team on its own channel.

| Key | Default | Meaning |
|---|---|---|
| `Meshtastic:Mqtt:Enabled` / `Password` | `false` (`true` in Development) / — | The API's service login to the broker (secret) |
| `Meshtastic:Mqtt:PublicHost` / `PublicPort` / `UseTls` | — / `1883` / `false` | Broker address shown in the gateway setup |
| `Meshtastic:Simulator:Enabled` | `false` (`true` in Development) | The fake multi-gateway mesh |
| `Meshtastic:Tcp:Host` / `Port` | — / `4403` | Optional local TCP node (user secrets only; was `Meshtastic:Gateway:*`) |
| `Meshtastic:Outbound:MinInterval` / `AckTimeout` | `00:00:10` / `00:01:30` | Duty cycle per gateway; delivery timeout |
| `Meshtastic:Retention:PositionDays` | `30` | Position history kept this many days |

All keys, the MQTT routing and how to set up a node: [Documentation/Mesh/README.md](Documentation/Mesh/README.md).

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
