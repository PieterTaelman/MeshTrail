using System.Net.Http.Json;
using Meshtrail.Core.Contracts.Samples;

namespace Meshtrail.Core.IntegrationTests.Samples;

internal static class SamplesApiTestHelpers
{
    public const string SamplesUrl = "/api/v1/samples";

    /// <summary>Unique per call, so tests sharing the database never see each other's rows.</summary>
    public static string UniqueName() => $"test-{Guid.NewGuid():N}";

    public static async Task<SampleDto> CreateSampleAsync(HttpClient client, string? name = null)
    {
        var response = await client.PostAsJsonAsync(SamplesUrl, new CreateSampleRequest(name ?? UniqueName(), null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SampleDto>())!;
    }
}
