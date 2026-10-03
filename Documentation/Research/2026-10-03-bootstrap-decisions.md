# Bootstrap decisions (2026-10-03)

Decisions taken when the skeleton was created, copying the CRT stack without its internal SysLib library.

| Topic | Decision | Notes |
|---|---|---|
| Mediator | martinothamar/Mediator 3 (source generated) | Replaces SysLib.Mediator. `ISender` in controllers, scoped lifetime |
| UI library | Optimus UI only | No AWF, no PrimeNG. Optimus is API-compatible with PrimeNG 21 |
| Authentication | JWT bearer skeleton + `ICurrentUser` | Identity provider to be chosen; `Development` mode fakes a user locally |
| Repository | One repo (backend + Client-Web) | No submodules |
| NuGet | nuget.org only | |
| CI | GitHub Actions | CD stub; deployment on-prem, to be defined |
| Logging sink | Not decided | Microsoft.Extensions.Logging + OpenTelemetry (OTLP) for now |
| Schema | SSDT `.sqlproj` | No EF migrations. AppHost rebuilds the local DB when the schema hash changes |
| Verify.MSTest | Not referenced yet | v33+ fails the build without a SponsorCheck licence property; needs a decision |

## Replacing SysLib

| SysLib piece | Replacement |
|---|---|
| SysLib.Mediator / IWebMediator / ICommandContext | Mediator + `ISender` + scoped `ICurrentUser` |
| SysLib.Application.WebHosting | `WebApplication.CreateBuilder` + `AddServiceDefaults()` |
| SysLib.Logger / Log4Net / RLB_LOG | Microsoft.Extensions.Logging + OpenTelemetry |
| SysLib.Application.OpenTelemetry / Diagnostics | ServiceDefaults + one `ActivitySource` per layer (`Meshtrail.*`) |
| SysLib.ServiceLocator | Constructor injection only |
| SysLib.Scheduling | `CronJob` (`BackgroundService` + Cronos) |
| SysLib.EventsService | `INotification` in-process + SignalR hub |
| technical-services AppHost resource | Removed |
