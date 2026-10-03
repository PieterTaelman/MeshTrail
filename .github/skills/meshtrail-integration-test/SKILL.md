---
name: meshtrail-integration-test
description: Use when writing or fixing Meshtrail API/integration tests — HTTP calls against the real WebApi via WebApplicationFactory<Program> and a real SQL Server built from the .sqlproj (Testcontainers, or MESHTRAIL_TEST_SQL for LocalDB/existing servers). Covers status codes, ProblemDetails, concurrency (409), auth, health endpoints. Triggers: "integration test", "API test", "test the endpoint", "test against the database", "WebApplicationFactory", "Testcontainers".
---

# Meshtrail integration tests

Project: `Code/Tests/Meshtrail.Core.IntegrationTests`. Worked example: `Samples/SamplesApiTests.cs` + `Samples/SamplesApiTestHelpers.cs`.

## How it works

- `AssemblySetup` (`[AssemblyInitialize]`) starts **one** SQL Server for the whole run and builds the schema with
  `DatabaseSchema` — the same code the AppHost uses, reading `Code/Database/Scripts_Core.txt`.
  - Default: Testcontainers `mcr.microsoft.com/mssql/server:2022-latest` (needs Docker).
  - `MESHTRAIL_TEST_SQL` set → uses that server instead (e.g. `Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true`).
  - The test database is always rebuilt at the start of a run (empty, no seed).
- `MeshtrailApiFactory : WebApplicationFactory<Program>` runs the real API in memory with environment `Testing`,
  the test connection string and `Authentication:Mode=Development` (user `integration-test`).
- Tests run **sequentially** (`[assembly: DoNotParallelize]`) and share the database: never assume an empty table.
  Use unique names (`UniqueName()`) and filter on them.

## Rules

- Go through HTTP only (`HttpClient` from `AssemblySetup.Factory.CreateClient()`); don't resolve repositories or the DbContext.
- Per endpoint: at least one happy path and the important error paths (400 validation with field key, 404, 409, 422).
- Deserialize into the real contract records (`ReadFromJsonAsync<SampleDto>()`), and `ValidationProblemDetails` for 400.
- Arrange/Act/Assert comments, Shouldly assertions, helpers in `<Feature>ApiTestHelpers.cs`.
- Name: `Endpoint_State_ExpectedStatus` (e.g. `Update_StaleRowVersion_Returns409`).

## Template

```csharp
[TestClass]
public sealed class <Feature>ApiTests
{
    private HttpClient _client = null!;

    [TestInitialize]
    public void Initialize() => _client = AssemblySetup.Factory.CreateClient();

    [TestCleanup]
    public void Cleanup() => _client.Dispose();

    [TestMethod]
    public async Task Create_EmptyName_Returns400WithFieldError()
    {
        // Act
        var response = await _client.PostAsJsonAsync(<Feature>Url, new Create<Feature>Request(string.Empty));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Keys.ShouldContain("Name");
    }
}
```

## Run

```bash
dotnet test --project Code/Tests/Meshtrail.Core.IntegrationTests                  # Docker
MESHTRAIL_TEST_SQL="Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true" \
  dotnet test --project Code/Tests/Meshtrail.Core.IntegrationTests                # no Docker
```

A new table that is missing from `Scripts_Core.txt` shows up here as "Invalid object name" — fix the list, not the test.
