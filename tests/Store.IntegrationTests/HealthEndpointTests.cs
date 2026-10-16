using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Store.Domain;
using Store.Infrastructure;

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
            using var factory = new TestApiFactory(fixture);
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

    [Fact]
    public async Task UnhandledException_ReturnsProblemDetailsAndPersistsAnErrorLog()
    {
        const string correlationId = "d58a135e-e84d-4b57-b263-9d84654f9d5f";
        const string connectionStringVariable = "ConnectionStrings__DefaultConnection";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringVariable);
        Environment.SetEnvironmentVariable(connectionStringVariable, fixture.ConnectionString);

        try
        {
            using var factory = new TestApiFactory(fixture)
                .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/test/throw?ignored=private");
            request.Headers.Add("X-Correlation-ID", correlationId);
            request.Headers.Add("Authorization", "Bearer sensitive-token");
            request.Content = JsonContent.Create(new { customer = "private@example.com" });

            using var response = await client.SendAsync(request);
            using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(correlationId, response.Headers.GetValues("X-Correlation-ID").Single());
            Assert.Equal(
                "unexpected_error",
                problem.RootElement.GetProperty("code").GetString());
            Assert.Equal(
                correlationId,
                problem.RootElement.GetProperty("correlationId").GetString());

            var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
            ErrorLog? savedError = null;
            var timeout = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < timeout && savedError is null)
            {
                await using var database = await contextFactory.CreateDbContextAsync();
                savedError = await database.ErrorLogs
                    .AsNoTracking()
                    .SingleOrDefaultAsync(errorLog => errorLog.CorrelationId == correlationId);

                if (savedError is null)
                {
                    await Task.Delay(100);
                }
            }

            Assert.NotNull(savedError);
            Assert.Equal("POST", savedError.HttpMethod);
            Assert.Equal("/test/throw", savedError.Path);
            Assert.Equal(500, savedError.StatusCode);
            Assert.Equal("An unhandled exception occurred.", savedError.Message);
            Assert.Equal(
                typeof(InvalidOperationException).FullName,
                savedError.ExceptionType);
            Assert.Null(savedError.OrderId);
            Assert.Null(savedError.PaymentProviderKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringVariable, previousConnectionString);
        }
    }

}
