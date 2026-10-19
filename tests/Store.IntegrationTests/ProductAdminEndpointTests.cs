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
    public async Task AdminEndpoints_RequireASession()
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
        using var duplicateProblem = await JsonDocument.ParseAsync(
            await duplicateResponse.Content.ReadAsStreamAsync());
        Assert.Equal("duplicate_sku", duplicateProblem.RootElement.GetProperty("code").GetString());

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
        Assert.Equal(
            "product_concurrency_conflict",
            conflict.RootElement.GetProperty("code").GetString());

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
    public async Task OrderHistoryEndpoint_RequiresAdminAndListsOrderStateAndPaymentProvider()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        using (var unauthorized = await client.GetAsync("/api/admin/orders"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        }

        using var customerClient = factory.CreateClient();
        await AuthTestClient.RegisterCustomerAsync(customerClient);
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        Guid customerId;
        await using (var lookup = await contextFactory.CreateDbContextAsync())
        {
            customerId = await lookup.Users.Where(user => user.Role == UserRole.Customer)
                .Select(user => user.Id).FirstAsync();
        }

        var order = new Order(
            customerId,
            $"ORD-2026-{Random.Shared.Next(100000, 999999)}",
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"),
            "history@example.test",
            new ShippingAddress("History customer", "1 Test Street", null, "Austin", "TX", "73301", "US"),
            Array.Empty<OrderItem>(),
            3.50m,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(15));
        Assert.True(order.MarkPaid(DateTime.UtcNow));
        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            database.Orders.Add(order);
            database.Payments.Add(new Payment(
                order.Id,
                "simulated",
                Guid.NewGuid().ToString("N"),
                "Approved",
                order.Total,
                DateTime.UtcNow));
            await database.SaveChangesAsync();
        }

        await SignInAsync(client, factory.AdminPassword);
        using var response = await client.GetAsync("/api/admin/orders?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var matchingOrder = body.RootElement.GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == order.Id);

        Assert.Equal("Paid", matchingOrder.GetProperty("state").GetString());
        Assert.Equal(3.50m, matchingOrder.GetProperty("shipping").GetDecimal());
        Assert.Equal(3.50m, matchingOrder.GetProperty("total").GetDecimal());
        Assert.Equal("simulated", matchingOrder.GetProperty("paymentProviderKey").GetString());
    }

    [Fact]
    public async Task E2eResetEndpoint_IsOnlyMappedWhenE2eModeIsEnabled()
    {
        using (var normalFactory = new TestApiFactory(fixture))
        using (var normalClient = normalFactory.CreateClient())
        using (var unavailable = await normalClient.PostAsync("/e2e/reset", null))
        {
            Assert.Contains(
                unavailable.StatusCode,
                new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        }

        using var e2eFactory = new TestApiFactory(fixture, e2eMode: true);
        using var e2eClient = e2eFactory.CreateClient();
        using var resetResponse = await e2eClient.PostAsync("/e2e/reset", null);
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

        await AuthTestClient.SignInAsync(e2eClient, TestApiFactory.AdminEmail, e2eFactory.AdminPassword);
        using var catalogResponse = await e2eClient.GetAsync("/api/catalog?q=E2E-ESPRESSO");
        Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);
        using var catalog = await JsonDocument.ParseAsync(
            await catalogResponse.Content.ReadAsStreamAsync());
        Assert.Contains(
            catalog.RootElement.GetProperty("items").EnumerateArray(),
            product => product.GetProperty("sku").GetString() == "E2E-ESPRESSO");
    }

    [Fact]
    public async Task Login_RejectsAnIncorrectAdminPassword()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/auth/login",
            new { email = TestApiFactory.AdminEmail, password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Task SignInAsync(HttpClient client, string adminPassword) =>
        AuthTestClient.SignInAsync(client, TestApiFactory.AdminEmail, adminPassword);

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

}
