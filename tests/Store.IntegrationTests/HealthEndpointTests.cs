using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Store.IntegrationTests;

public sealed class HealthEndpointTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Health_ReturnsHealthy_WhenPostgreSqlIsAvailable()
    {
        const string connectionStringVariable = "ConnectionStrings__DefaultConnection";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringVariable);
        Environment.SetEnvironmentVariable(connectionStringVariable, fixture.ConnectionString);

        try
        {
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.Contains("X-Correlation-ID"));

            await using var content = await response.Content.ReadAsStreamAsync();
            using var body = await JsonDocument.ParseAsync(content);
            Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringVariable, previousConnectionString);
        }
    }
}
