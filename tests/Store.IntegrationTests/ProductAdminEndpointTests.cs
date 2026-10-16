using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Store.Application;
using Store.Api.Admin;
using Store.Domain;
using Store.Infrastructure;

namespace Store.IntegrationTests;

public sealed class ProductAdminEndpointTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task AdminEndpoints_RequireBearerToken()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        var requests = new[]
        {
            new HttpRequestMessage(HttpMethod.Get, "/api/admin/products"),
            new HttpRequestMessage(HttpMethod.Post, "/api/admin/products")
            {
                Content = JsonContent.Create(CreateProduct("No auth", "NO-AUTH"))
            },
            new HttpRequestMessage(HttpMethod.Get, $"/api/admin/products/{Guid.NewGuid()}"),
            new HttpRequestMessage(HttpMethod.Put, $"/api/admin/products/{Guid.NewGuid()}")
            {
                Content = JsonContent.Create(CreateProduct("No auth", "NO-AUTH"))
            },
            new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/products/{Guid.NewGuid()}"),
            new HttpRequestMessage(HttpMethod.Get, "/api/admin/error-logs")
        };

        foreach (var request in requests)
        {
            using (request)
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }
    }

    [Fact]
    public async Task ProductEndpoints_CreateReadUpdateAndSoftDeleteWithOptimisticConcurrency()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, factory.AdminPassword);

        using var createResponse = await client.PostAsJsonAsync(
            "/api/admin/products",
            CreateProduct("Tea kettle", "Kettle-01"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(created);
        Assert.NotEqual(0u, created.Version);

        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/admin/products",
            CreateProduct("Duplicate", "  kettle-01 "));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using var listResponse = await client.GetAsync("/api/admin/products?page=1&pageSize=10");
        var page = await listResponse.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>();
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains(page!.Items, item => item.Id == created.Id);

        using var readResponse = await client.GetAsync($"/api/admin/products/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        var firstRead = await readResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.Equal(created.Version, firstRead!.Version);

        var changed = CreateProduct("Tea kettle updated", "Kettle-01", firstRead.Version);
        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/products/{created.Id}",
            changed);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotEqual(firstRead.Version, updated!.Version);

        using var staleResponse = await client.PutAsJsonAsync(
            $"/api/admin/products/{created.Id}",
            CreateProduct("Stale update", "Kettle-01", firstRead.Version));
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        using var conflict = await JsonDocument.ParseAsync(
            await staleResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Product was modified", conflict.RootElement.GetProperty("title").GetString());

        using var deleteResponse = await client.DeleteAsync($"/api/admin/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        using var deletedRead = await client.GetAsync($"/api/admin/products/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deletedRead.StatusCode);

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        var retained = await database.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(product => product.Id == created.Id);
        Assert.True(retained.IsDeleted);
        Assert.NotEqual(0u, retained.Version);
    }

    [Fact]
    public async Task ErrorLogEndpoint_RequiresAdminAndAppliesFilters()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();
        using (var unauthorized = await client.GetAsync("/api/admin/error-logs"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        }

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            database.ErrorLogs.AddRange(
                MakeLog("correlation-match", "Error"),
                MakeLog("correlation-other", "Warning"));
            await database.SaveChangesAsync();
        }

        await SignInAsync(client, factory.AdminPassword);
        var fromUtc = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"));
        var toUtc = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"));
        using var response = await client.GetAsync(
            $"/api/admin/error-logs?fromUtc={fromUtc}&toUtc={toUtc}&level=Error&correlationId=correlation-match&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<ErrorLogPage>();
        Assert.Equal(1, page!.TotalCount);
        Assert.Equal("correlation-match", Assert.Single(page.Items).CorrelationId);
    }

    [Fact]
    public async Task Login_RejectsAnIncorrectAdminPassword()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/auth/login", new { password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task SignInAsync(HttpClient client, string adminPassword)
    {
        using var response = await client.PostAsJsonAsync(
            "/auth/login",
            new { password = adminPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(token);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }

    private static ProductRequest CreateProduct(
        string name,
        string sku,
        uint? version = null) =>
        new(name, sku, "Description", "Kitchen", 19.95m, 8, 1.25m, version);

    private static ErrorLog MakeLog(string correlationId, string level) => new()
    {
        TimestampUtc = DateTime.UtcNow,
        Level = level,
        CorrelationId = correlationId,
        Message = "Test error",
        ExceptionType = "System.Exception",
        HttpMethod = "GET",
        Path = "/test",
        StatusCode = 500
    };

    private sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc);
}
