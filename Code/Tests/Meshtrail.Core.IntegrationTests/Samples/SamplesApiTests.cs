using System.Net;
using System.Net.Http.Json;
using Meshtrail.Core.Contracts.Common;
using Meshtrail.Core.Contracts.Samples;
using Meshtrail.Core.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using static Meshtrail.Core.IntegrationTests.Samples.SamplesApiTestHelpers;

namespace Meshtrail.Core.IntegrationTests.Samples;

[TestClass]
public sealed class SamplesApiTests
{
    private HttpClient _client = null!;

    [TestInitialize]
    public void Initialize() => _client = AssemblySetup.Factory.CreateClient();

    [TestCleanup]
    public void Cleanup() => _client.Dispose();

    [TestMethod]
    public async Task Create_ValidRequest_Returns201AndCanBeLoaded()
    {
        // Arrange
        var request = new CreateSampleRequest(UniqueName(), "  created by a test  ");

        // Act
        var response = await _client.PostAsJsonAsync(SamplesUrl, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<SampleDto>();
        created.ShouldNotBeNull();
        created.Description.ShouldBe("created by a test");
        created.CreatedBy.ShouldBe(MeshtrailApiFactory.TestUser);
        created.RowVersion.ShouldNotBeNullOrEmpty();

        var loaded = await _client.GetFromJsonAsync<SampleDto>(response.Headers.Location);
        loaded.ShouldBe(created);
    }

    [TestMethod]
    public async Task Create_EmptyName_Returns400WithFieldError()
    {
        // Act
        var response = await _client.PostAsJsonAsync(SamplesUrl, new CreateSampleRequest(string.Empty, null));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.ShouldNotBeNull();
        problem.Errors.Keys.ShouldContain("Name");
    }

    [TestMethod]
    public async Task GetById_UnknownId_Returns404()
    {
        // Act
        var response = await _client.GetAsync($"{SamplesUrl}/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Update_CurrentRowVersion_Returns200WithNewRowVersion()
    {
        // Arrange
        var created = await CreateSampleAsync(_client);

        // Act
        var response = await _client.PutAsJsonAsync($"{SamplesUrl}/{created.Id}", new UpdateSampleRequest("Renamed " + created.Name, null, created.RowVersion));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<SampleDto>();
        updated.ShouldNotBeNull();
        updated.Name.ShouldStartWith("Renamed ");
        updated.ModifiedBy.ShouldBe(MeshtrailApiFactory.TestUser);
        updated.RowVersion.ShouldNotBe(created.RowVersion);
    }

    [TestMethod]
    public async Task Update_StaleRowVersion_Returns409()
    {
        // Arrange: two edits start from the same version; the first one wins.
        var created = await CreateSampleAsync(_client);
        var first = await _client.PutAsJsonAsync($"{SamplesUrl}/{created.Id}", new UpdateSampleRequest("First edit", null, created.RowVersion));
        first.EnsureSuccessStatusCode();

        // Act
        var second = await _client.PutAsJsonAsync($"{SamplesUrl}/{created.Id}", new UpdateSampleRequest("Second edit", null, created.RowVersion));

        // Assert
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Delete_ExistingSample_Returns204AndIsGone()
    {
        // Arrange
        var created = await CreateSampleAsync(_client);

        // Act
        var response = await _client.DeleteAsync($"{SamplesUrl}/{created.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.GetAsync($"{SamplesUrl}/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GetGrid_SearchAndPaging_ReturnsMatchingPage()
    {
        // Arrange: three samples share a unique marker so other tests' rows do not interfere.
        var marker = UniqueName();
        foreach (var suffix in new[] { "a", "b", "c" })
        {
            await CreateSampleAsync(_client, $"{marker}-{suffix}");
        }

        // Act
        var page = await _client.GetFromJsonAsync<PagedResult<SampleGridItemDto>>(
            $"{SamplesUrl}?search={marker}&pageSize=2&page=1&sortBy={SampleGridSortColumns.Name}");

        // Assert
        page.ShouldNotBeNull();
        page.TotalCount.ShouldBe(3);
        page.Items.Select(item => item.Name).ShouldBe([$"{marker}-a", $"{marker}-b"]);
    }

    [TestMethod]
    public async Task GetGrid_UnknownSortColumn_Returns400()
    {
        // Act
        var response = await _client.GetAsync($"{SamplesUrl}?sortBy=nonsense");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task GetFilterOptions_AfterCreate_ContainsCurrentUser()
    {
        // Arrange
        await CreateSampleAsync(_client);

        // Act
        var options = await _client.GetFromJsonAsync<SampleFilterOptionsDto>($"{SamplesUrl}/filter-options");

        // Assert
        options.ShouldNotBeNull();
        options.CreatedBy.ShouldContain(MeshtrailApiFactory.TestUser);
    }
}
