# AGENTS.md — Meshtrail

Single source of truth for AI assistants (Copilot, OpenCode, Claude Code). `CLAUDE.md` only imports this file.
Do not copy content from here into other files — link to it.

> **⛔ Hard rule: never add a `SysLib.*` package.** It is an internal library we may not use. The build fails if one
> is referenced (`Directory.Build.targets`). Do not add MediatR either: the mediator is **martinothamar/Mediator v3**.

The business domain is not described yet. The `Samples` module is a **placeholder reference module**: copy its shape
for new features; it will be renamed or removed once the real domain arrives.

## Tech stack

| Area | Choice |
|---|---|
| Runtime | .NET 10 (`net10.0`), C# `latest`, nullable + implicit usings, warnings are errors |
| Packages | Central Package Management (`Directory.Packages.props`, transitive pinning), nuget.org only |
| API | ASP.NET Core controllers, `Asp.Versioning` (`api/v{version}/…`), OpenAPI + Scalar at `/scalar` |
| Mediator | `Mediator.Abstractions` + `Mediator.SourceGenerator` 3.x (source generator **only** in `Meshtrail.WebApi`) |
| Validation | FluentValidation 12, run by `ValidationBehavior` |
| Data | EF Core 10 on SQL Server. **Schema = SSDT `.sqlproj`, no EF migrations** |
| Realtime | SignalR hub `/hubs/notifications` (server → client pushes only) |
| Jobs | `CronJob` base class (`BackgroundService` + Cronos) |
| Telemetry | Microsoft.Extensions.Logging + OpenTelemetry (OTLP). Production sink: not decided yet |
| Auth | JWT bearer skeleton + `ICurrentUser`. Identity provider: not decided yet. `Development` mode fakes a user locally |
| Tests | MSTest.Sdk 4 (Microsoft.Testing.Platform), Shouldly, Moq, `WebApplicationFactory`, Testcontainers |
| Frontend | Angular 22, TypeScript 6 strict, Signals, Optimus UI, Tailwind 4, Vitest, Playwright — see `Client-Web/AGENTS.md` |
| Local dev | .NET Aspire 13.6 AppHost (local only; deployment is on-prem), Docker for SQL Server + MailPit |
| CI | GitHub Actions (`.github/workflows/ci.yml`) |

## Reference documentation

| Doc | What it covers |
|---|---|
| [README.md](README.md) | Getting started, prerequisites, running the AppHost, SSMS |
| [Documentation/README.md](Documentation/README.md) | Index of living docs |
| [Documentation/Mesh/README.md](Documentation/Mesh/README.md) | The Meshtastic gateway, simulator and how to connect a real node |
| [Documentation/Samples/README.md](Documentation/Samples/README.md) | The reference module end-to-end (class diagram, DB schema, request flow) |
| [Documentation/Research/](Documentation/Research/) | Point-in-time decision records (history, not kept in sync) |
| [Client-Web/AGENTS.md](Client-Web/AGENTS.md) | Angular standards |

**Living docs rule:** when you change behaviour that a doc in `Documentation/` describes, update that doc in the
same change. `Documentation/Research/` is history and is never updated afterwards.

## Solution layout

```
Code/Libraries/Meshtrail.Core.Domain          entities, value objects, business rules — depends on nothing
Code/Libraries/Meshtrail.Core.Contracts       request/response records of the API — depends on nothing
Code/Libraries/Meshtrail.Core.Application     use cases (commands/queries/handlers/validators), repository interfaces,
                                              behaviors, ICurrentUser, domain → contract mapping
Code/Libraries/Meshtrail.Core.Infrastructure  EF DbContext, Db* entities, mappers, repositories, cron jobs
Code/Libraries/Meshtrail.Mesh                 Meshtastic protocol: vendored protobufs, framing, IMeshRadio (TCP +
                                              simulator), contact links — depends on nothing from Core
Code/Server/Meshtrail.WebApi                  composition root + controllers only (Mediator source generator lives here)
Code/Server/Meshtrail.ServiceDefaults         OpenTelemetry, health checks, resilience, service discovery
Code/Server/Meshtrail.AppHost                 Aspire orchestrator (local development only)
Code/Tools/Meshtrail.MeshProbe                console tool: connect to a node and print what it says
Code/Tests/Meshtrail.Core.UnitTests           domain, handlers, validators, behaviors — no DB, no HTTP
Code/Tests/Meshtrail.Core.IntegrationTests    API tests against a real SQL Server
Code/Database/Meshtrail.Database              SSDT project: tables + seed scripts
Code/Database/Scripts_Core.txt                table scripts in dependency order (AppHost + tests read this)
Code/Database/Tooling                         schema builder shared (linked source) by AppHost and integration tests
Client-Web/                                   Angular app
```

Dependencies point inward: WebApi → Application → Domain; Infrastructure → Application/Domain. Domain references nothing.

## Build & test

```bash
dotnet build Meshtrail.slnx                                            # must end with 0 warnings
dotnet test --project Code/Tests/Meshtrail.Core.UnitTests
dotnet test --project Code/Tests/Meshtrail.Core.IntegrationTests       # Docker, or MESHTRAIL_TEST_SQL (see below)
dotnet run --project Code/Server/Meshtrail.AppHost                     # whole stack locally

cd Client-Web && npm ci && npm run lint && npm test && npm run build
```

### Gotchas

- **Don't `dotnet build` the `.sqlproj`.** It is a classic SSDT project and needs Visual Studio MSBuild + SSDT. The
  `.slnx` marks it `Build Project="false"`, so `dotnet build Meshtrail.slnx` skips it. Build it from Visual Studio.
- **New table?** Add the script to the `.sqlproj` **and** to `Code/Database/Scripts_Core.txt`, or the AppHost and tests
  will not create it. See the `meshtrail-database-table` skill.
- The AppHost **drops and rebuilds** the local database when any listed table script changes (hash check). Data you
  need across schema changes belongs in a seed script.
- Integration tests use Testcontainers (Docker). Without Docker set `MESHTRAIL_TEST_SQL` to a server connection string,
  e.g. `Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true`.
- `Mediator.SourceGenerator` may only be referenced by `Meshtrail.WebApi` (enforced in `Directory.Build.targets`).
- The `AddMediator((MediatorOptions o) => …)` lambda is read by the source generator at compile time: keep it a plain literal.
- Angular 22 needs **Node ≥ 22.22.3 or ≥ 24.15** (`npm start` fails on older Node).
- Use `127.0.0.1,14330` (not `localhost`) in SSMS.
- Verify (`Verify.MSTest`) is listed in CPM but not referenced: v33+ needs a SponsorCheck licence decision first.

## C# conventions

- File-scoped namespaces, primary constructors, `sealed` by default, records for contracts and messages.
- Guard clauses and pattern matching over nested `if`s. `var` where the type is obvious.
- Async all the way, every async method takes a `CancellationToken` and passes it on.
- Logging with `[LoggerMessage]` source-generated methods (no string interpolation in log calls).
- **Comments are short and written for a junior developer**: plain language, one sentence, explain *why*.
- Business rules live in the domain entity. Handlers orchestrate: load → `KeyNotFoundException` if missing → call
  domain → persist → publish notification → map to contract.
- Validators are shape-only (required, length, non-empty Guid) and `sealed : AbstractValidator<T>`.
- Exceptions → HTTP (in `GlobalExceptionHandler`): `ValidationException` 400, `KeyNotFoundException` 404,
  `ConcurrencyException` 409, `DomainException` 422.
- Handlers never touch `HttpContext`; inject `ICurrentUser` and `TimeProvider`.

Full layer checklist with Mediator specifics: [`meshtrail-architecture` skill](.github/skills/meshtrail-architecture/SKILL.md).

## Testing

- Unit tests: MSTest + Shouldly + Moq, Arrange/Act/Assert comments, one behaviour per test, helpers in a separate
  `*TestHelpers.cs` file. No database, no HTTP.
- Integration tests: real API (`WebApplicationFactory<Program>`) + real SQL Server built from the `.sqlproj`.
  Use unique names per test; the database is shared within a run.
- Test names: `Method_State_ExpectedResult`.

## Skills routing

Read the matching `SKILL.md` **before** starting the task.

| When you are… | Skill |
|---|---|
| adding/changing an endpoint, command, query, handler, validator, repository, entity, contract | [meshtrail-architecture](.github/skills/meshtrail-architecture/SKILL.md) |
| adding/changing a table, column, FK, index or seed script | [meshtrail-database-table](.github/skills/meshtrail-database-table/SKILL.md) |
| writing unit tests | [meshtrail-unit-test](.github/skills/meshtrail-unit-test/SKILL.md) |
| writing API/integration tests | [meshtrail-integration-test](.github/skills/meshtrail-integration-test/SKILL.md) |
| adding a scheduled/background job | [meshtrail-background-job](.github/skills/meshtrail-background-job/SKILL.md) |
| writing or maintaining docs in `Documentation/` | [technical-documentation](.github/skills/technical-documentation/SKILL.md) |
| creating a new skill | [make-skill-template](.github/skills/make-skill-template/SKILL.md) |
| working on the Angular app | [angular-development](.github/skills/angular-development/SKILL.md) + [Client-Web/AGENTS.md](Client-Web/AGENTS.md) |

Agents: [angular-expert](.github/agents/angular-expert.md), [specification](.github/agents/specification.md).

## Commit & PR standards

- Conventional Commits: `type(scope): description` — lowercase, imperative, ≤ 100 characters per line.
  Types: `feat`, `fix`, `refactor`, `test`, `docs`, `build`, `ci`, `chore`, `perf`.
  Scopes: `api`, `app` (Application), `domain`, `infra`, `db`, `apphost`, `web` (Angular), `ci`, `docs`.
- Never commit plans or progress notes as separate commits — put them in the PR description.
- A PR is ready when `dotnet build` has 0 warnings, all tests are green, `npm run lint` is clean and touched docs are updated.
