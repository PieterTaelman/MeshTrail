---
name: meshtrail-background-job
description: Use when adding or changing a scheduled or recurring background job in Meshtrail (cron schedule, nightly cleanup, periodic sync, polling, recurring report). Jobs derive from the CronJob base class (BackgroundService + Cronos), get a fresh DI scope per run, are configured under Jobs:<Name> and dispatch use cases through ISender. Triggers: "background job", "scheduled task", "cron", "run every X minutes", "nightly job", "recurring".
---

# Meshtrail background job

Base class: `Code/Libraries/Meshtrail.Core.Infrastructure/Jobs/CronJob.cs` (replaces SysLib.Scheduling).
Worked example: `Jobs/SampleStatisticsJob.cs`.

## How `CronJob` behaves

- Reads `CronJobOptions` from configuration `Jobs:<JobName>`: `Enabled`, `Cron` (5-field), optional `TimeZone`.
- Disabled → logs once and does nothing. Enabled → waits for the next cron occurrence, runs, repeats.
- Each run gets its **own DI scope**, so scoped services (`ISender`, repositories, `DbContext`) work like in a request.
- An exception in a run is logged and swallowed; the schedule continues. Shutdown cancels cleanly.
- Runs inside the WebApi process. If several API instances run on-prem, every instance runs the job — make the work
  idempotent or add a lock (e.g. `sp_getapplock`) before it matters.

## Steps

1. **Put the logic in a use case** (command/query + handler in Application), not in the job. The job only dispatches it.
   Then it is validated, logged, traced and unit-testable. See the `meshtrail-architecture` skill.
2. **Job class** `Code/Libraries/Meshtrail.Core.Infrastructure/Jobs/<Name>Job.cs`:
   ```csharp
   internal sealed partial class <Name>Job(
       IServiceScopeFactory scopeFactory,
       IOptionsMonitor<CronJobOptions> options,
       TimeProvider timeProvider,
       ILogger<<Name>Job> logger)
       : CronJob(scopeFactory, options, timeProvider, logger)
   {
       public const string Name = "<Name>";

       protected override string JobName => Name;

       protected override async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
       {
           var sender = services.GetRequiredService<ISender>();
           await sender.Send(new <Something>Command(), cancellationToken);
       }
   }
   ```
3. **Register** in `DependencyInjection.AddInfrastructure`:
   `services.AddCronJob<<Name>Job>(configuration, <Name>Job.Name);`
4. **Configure** in `Code/Server/Meshtrail.WebApi/appsettings.json` (disabled by default) and enable where needed:
   ```json
   "Jobs": { "<Name>": { "Enabled": false, "Cron": "0 2 * * *", "TimeZone": "Romance Standard Time" } }
   ```
   Integration tests run in environment `Testing`, which only loads `appsettings.json` — keep jobs disabled there.
5. **Test** the use case with unit tests (`meshtrail-unit-test`). The job class itself has no logic to test.
6. **Document** the job (schedule + purpose) in the relevant `Documentation/` page.

## Cron cheat sheet (Cronos, 5 fields: minute hour day month weekday)

| Expression | Meaning |
|---|---|
| `*/5 * * * *` | every 5 minutes |
| `0 * * * *` | every hour on the hour |
| `0 2 * * *` | every day at 02:00 |
| `0 6 * * 1-5` | weekdays at 06:00 |
