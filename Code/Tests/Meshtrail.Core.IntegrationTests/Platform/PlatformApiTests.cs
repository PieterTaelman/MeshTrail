using System.Net;
using Shouldly;

namespace Meshtrail.Core.IntegrationTests.Platform;

/// <summary>Checks the cross-cutting endpoints every deployment relies on.</summary>
[TestClass]
public sealed class PlatformApiTests
{
    [TestMethod]
    [DataRow("/health/live")]
    [DataRow("/health/ready")]
    [DataRow("/openapi/v1.json")]
    public async Task Endpoint_IsReachableWithoutLogin(string url)
    {
        // Arrange
        using var client = AssemblySetup.Factory.CreateClient();

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
