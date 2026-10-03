---
name: meshtrail-unit-test
description: Use when writing or fixing unit tests in Meshtrail — domain entity rules, Mediator handlers, FluentValidation validators, pipeline behaviors, mappers. MSTest.Sdk 4 + Shouldly + Moq, Arrange/Act/Assert, helpers in a separate *TestHelpers.cs file, no database and no HTTP. Triggers: "write a unit test", "test this handler", "add tests for the validator", "cover this domain rule", "mock the repository".
---

# Meshtrail unit tests

Project: `Code/Tests/Meshtrail.Core.UnitTests` (MSTest.Sdk, runs on Microsoft.Testing.Platform, methods run in parallel).
Worked examples: `Samples/SampleTests.cs` (domain), `Samples/SampleHandlerTests.cs` (handlers),
`Samples/SampleValidatorTests.cs`, `Behaviors/ValidationBehaviorTests.cs`, shared data in `Samples/SampleTestHelpers.cs`.

## Rules

- **No database, no HTTP, no file system, no real clock.** Mock `I…Repository`, `ICurrentUser`, `IPublisher`;
  use a fixed `TimeProvider` (see `SampleTestHelpers.FixedTime()`).
- One behaviour per test. Name: `Method_State_ExpectedResult` (e.g. `Update_StaleRowVersion_Throws`).
- `// Arrange`, `// Act`, `// Assert` comments (`// Act + Assert` for `Should.Throw`).
- Assertions with **Shouldly** (`result.ShouldBe(...)`, `Should.ThrowAsync<T>(...)`), never `Assert.*`.
- Test classes `public sealed class …Tests` with `[TestClass]`; use `[DataRow]` for input variations.
- Builders, fixed values and mock factories go in `<Feature>TestHelpers.cs` (`internal static class`), imported with
  `using static`. Keep test files about the behaviour, not the setup.
- Internal types are visible to this project (`InternalsVisibleTo` in `Directory.Build.props`).

## What to test where

| Target | Test |
|---|---|
| Domain entity | Every invariant (happy path + each `DomainException`), trimming/normalisation, audit fields |
| Handler | Orchestration: not found → `KeyNotFoundException` and **no save**; saves once; publishes the right notification; maps correctly; passes the right values (e.g. client row version) to the repository |
| Validator | Valid command has no errors; each rule reports the right `PropertyName` |
| Behavior | Calls `next` when allowed, short-circuits otherwise |

Do **not** unit test controllers, EF configuration or repositories — integration tests cover those.

## Template

```csharp
using Moq;
using Shouldly;
using static Meshtrail.Core.UnitTests.<Feature>.<Feature>TestHelpers;

namespace Meshtrail.Core.UnitTests.<Feature>;

[TestClass]
public sealed class <UseCase>HandlerTests
{
    [TestMethod]
    public async Task Handle_UnknownId_ThrowsKeyNotFoundAndDoesNotSave()
    {
        // Arrange
        var repository = RepositoryReturning(null);
        var handler = new <UseCase>Handler(repository.Object, CurrentUser().Object, FixedTime(), Publisher().Object);

        // Act + Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await handler.Handle(new <UseCase>Command(Guid.NewGuid(), "Name"), CancellationToken.None));
        repository.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

Handlers return `ValueTask`: `await handler.Handle(...)` directly; inside `Should.ThrowAsync` wrap it in an async lambda.

## Run

```bash
dotnet test --project Code/Tests/Meshtrail.Core.UnitTests
dotnet test --project Code/Tests/Meshtrail.Core.UnitTests --filter-method "*SampleHandlerTests*"
```
