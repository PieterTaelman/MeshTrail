---
name: meshtrail-architecture
description: Use when adding or changing anything in the Meshtrail backend request path — a new endpoint, controller action, command, query, handler, validator, pipeline behavior, notification, repository (interface or implementation), domain entity, value object, business rule or API contract/DTO. Covers the full DDD/CQRS layer checklist on martinothamar/Mediator v3 (ValueTask handlers, ISender, ICurrentUser, scoped lifetime, source generator only in WebApi). Triggers: "add an endpoint", "new use case", "create a command/query", "add CRUD for X", "implement feature X", "new entity".
---

# Meshtrail architecture — layer checklist

Worked example for every step: the **Samples** module ([Documentation/Samples/README.md](../../../Documentation/Samples/README.md)).
Copy its files and rename; do not invent a new shape.

## Request flow

```
Controller ──ISender.Send──▶ Command/Query ──▶ ValidationBehavior ──▶ LoggingBehavior ──▶ Handler
                                                                                      │
                                                     Domain entity (business rules)    │
                                                     Repository (interface in Application, impl in Infrastructure)
                                                                                      │
                                                     Contract record ◀── mapper ◀─────┘
```

## Checklist for a new feature `<Feature>` / use case `<UseCase>`

### 1. Domain — `Code/Libraries/Meshtrail.Core.Domain/<Feature>/`
- `sealed` entity, private parameterless constructor, `private set` properties.
- `static Create(...)` factory for new instances, `static Rehydrate(...)` for loading from storage.
- Behaviour methods (`Update`, `Rename`, `Close` …) enforce invariants and throw `DomainException` (→ 422).
- Length limits as `public const int` so validators and EF config reuse them.
- No references to any package or other project. Time and user come in as parameters.
- Example: `Samples/Sample.cs`.

### 2. Contracts — `Code/Libraries/Meshtrail.Core.Contracts/<Feature>/`
- `sealed record` request/response types (`<Feature>Dto`, `Create<Feature>Request`, `<Feature>GridRequest` …).
- Concurrency tokens travel as base64 `string RowVersion`.
- Lists return `PagedResult<T>`. Example: `Samples/SampleContracts.cs`.

### 3. Application — `Code/Libraries/Meshtrail.Core.Application/`
- Folder per use case: `UseCases/<Feature>/Commands/<UseCase>/{<UseCase>Command, <UseCase>Handler, <UseCase>Validator}.cs`
  (queries under `Queries/`).
- Message: `public sealed record <UseCase>Command(...) : ICommand<TResponse>` (no result: `ICommand`; read: `IQuery<T>`).
- Handler:
  ```csharp
  public sealed class <UseCase>Handler(I<Feature>Repository repository, ICurrentUser currentUser, TimeProvider time, IPublisher publisher)
      : ICommandHandler<<UseCase>Command, <Feature>Dto>
  {
      public async ValueTask<<Feature>Dto> Handle(<UseCase>Command command, CancellationToken cancellationToken)
      {
          var entity = await repository.GetByIdAsync(command.Id, cancellationToken)
              ?? throw new KeyNotFoundException($"<Feature> {command.Id} does not exist.");
          entity.DoSomething(..., currentUser.Name, time.GetUtcNow());
          await repository.UpdateAsync(entity, ..., cancellationToken);
          await repository.SaveChangesAsync(cancellationToken);
          await publisher.Publish(new <Feature>ChangedNotification(entity.Id, ...), cancellationToken);
          return entity.ToDto();
      }
  }
  ```
  - Return type is **`ValueTask<T>`**; `ICommand` handlers return `ValueTask<Unit>` (`return Unit.Value;`).
  - Pass the `CancellationToken` to **every** async call.
  - Never use `HttpContext`/`IHttpContextAccessor`; use `ICurrentUser`. Never `DateTime.Now`; use `TimeProvider`.
  - Publish notifications **after** `SaveChangesAsync`.
- Validator: `public sealed class <UseCase>Validator : AbstractValidator<<UseCase>Command>` — shape only
  (`NotEmpty`, `MaximumLength(<Entity>.NameMaxLength)`, non-empty Guid, base64). It runs automatically; never call it yourself.
- Repository interface: `Repositories/I<Feature>Repository.cs` — async + `CancellationToken`:
  `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `SaveChangesAsync`, and for list screens
  `GetGridAsync(<Feature>GridRequest)` / `GetFilterOptionsAsync` returning contracts directly (read side).
- Mapping domain → contract: `UseCases/<Feature>/<Feature>Mappings.cs` (`internal static ToDto()`).
- Notifications: `record ... : INotification` + `sealed class ... : INotificationHandler<T>` returning `ValueTask`.

### 4. Infrastructure — `Code/Libraries/Meshtrail.Core.Infrastructure/`
- `Persistence/Entities/Db<Feature>.cs` (`internal sealed`, plain properties).
- `Persistence/Configurations/Db<Feature>Configuration.cs` — table/column mapping that matches the `.sqlproj`;
  `IsRowVersion()` for the version column; `ValueGeneratedNever()` for domain-generated ids.
- `Persistence/Mappers/<Feature>Mapper.cs` — `ToDomain`, `ToDb`, `CopyTo`. Nothing EF leaks past Infrastructure.
- `Repositories/<Feature>Repository.cs` — translate `DbUpdateConcurrencyException` into `ConcurrencyException` (→ 409).
- Register in `DependencyInjection.AddInfrastructure`: `services.AddScoped<I<Feature>Repository, <Feature>Repository>();`
- Table + `Scripts_Core.txt`: see the `meshtrail-database-table` skill.

### 5. WebApi — `Code/Server/Meshtrail.WebApi/Controllers/<Feature>Controller.cs`
```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/<features>")]
public sealed class <Feature>Controller(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<<Feature>Dto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new Get<Feature>ByIdQuery(id), cancellationToken));
}
```
- Inject **`ISender`** (not `IMediator`). Only map HTTP ↔ message. No try/catch, no logic.
- Errors are mapped by `GlobalExceptionHandler`: `ValidationException` 400, `KeyNotFoundException` 404,
  `ConcurrencyException` 409, `DomainException` 422.
- Endpoints require an authenticated user by default (fallback policy). Opt out only with `[AllowAnonymous]` and a reason.

### 6. Tests
- Unit: domain rules, handler orchestration, validators → `meshtrail-unit-test` skill.
- Integration: one happy path + one error path per endpoint → `meshtrail-integration-test` skill.

### 7. Docs
- Update `Documentation/` if behaviour described there changed (living docs rule).

## Mediator rules (differs from CRT — get these right)

- Packages: only `Meshtrail.WebApi` references `Mediator.SourceGenerator`; all other projects reference
  `Mediator.Abstractions`. The build fails otherwise (`Directory.Build.targets`).
- Registration is in `Program.cs`:
  ```csharp
  builder.Services.AddMediator((MediatorOptions options) =>
  {
      options.ServiceLifetime = ServiceLifetime.Scoped;
      options.Assemblies = [typeof(ApplicationAssemblyMarker)];
      options.PipelineBehaviors = [typeof(ValidationBehavior<,>), typeof(LoggingBehavior<,>)];
  });
  ```
  The generator reads this lambda at compile time — keep it a literal. New behaviors go in `PipelineBehaviors`,
  **not** in DI as open generics.
- Behavior signature: `ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)`
  and call `await next(message, cancellationToken)`.
- Handlers outside the Application assembly are **not** discovered (only `ApplicationAssemblyMarker`'s assembly is scanned).
- There is no `ICommandContext`, no MediatR, no SysLib.

## Don'ts

- ❌ Business rules in handlers or validators (put them in the entity).
- ❌ `DbContext` or `Db*` types outside Infrastructure.
- ❌ Returning domain entities from the API (always contracts).
- ❌ `async void`, `.Result`, `.Wait()`, missing `CancellationToken`.
- ❌ EF migrations — the `.sqlproj` is the schema.
