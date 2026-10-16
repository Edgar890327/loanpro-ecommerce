using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Store.Api.Admin;

namespace Store.IntegrationTests;

public sealed class ShippingSettingsEndpointTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task SettingsRequireAdminAndShippingQuoteChangesWithoutChangingExistingOrders()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();
        using var unauthorized = await client.GetAsync("/api/admin/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        await SignInAsync(client, factory.AdminPassword);

        using var settingsResponse = await client.GetAsync("/api/admin/settings");
        Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
        using var settings = JsonDocument.Parse(await settingsResponse.Content.ReadAsStringAsync());
        Assert.Contains(
            settings.RootElement.GetProperty("shippingStrategies").EnumerateArray(),
            strategy => strategy.GetProperty("configurationFields").EnumerateArray()
                .Any(field => field.GetProperty("key").GetString() == "amountUsd"));

        var productId = await CreateProductAsync(client);
        using var publicClient = factory.CreateClient();
        var initialQuote = await QuoteAsync(publicClient, productId);
        Assert.Equal(0m, initialQuote);

        await SaveShippingRateAsync(client, "12.50");
        Assert.Equal(12.50m, await QuoteAsync(publicClient, productId));

        var order = await CreateOrderAsync(publicClient, productId);
        Assert.Equal(12.50m, order.Shipping);
        await SaveShippingRateAsync(client, "6.25");
        Assert.Equal(6.25m, await QuoteAsync(publicClient, productId));

        using var orderResponse = await publicClient.GetAsync($"/api/orders/{order.Id}");
        using var orderJson = JsonDocument.Parse(await orderResponse.Content.ReadAsStringAsync());
        Assert.Equal(12.50m, orderJson.RootElement.GetProperty("shipping").GetDecimal());
    }

    private static async Task<decimal> QuoteAsync(HttpClient client, Guid productId)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/shipping/quote",
            new
            {
                items = new[] { new { productId, quantity = 1 } },
                destination = "100 Main Street"
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("shipping").GetDecimal();
    }

    private static async Task SaveShippingRateAsync(HttpClient client, string amount)
    {
        using var response = await client.PutAsJsonAsync(
            "/api/admin/settings/shipping/flat-rate",
            new
            {
                publicConfiguration = new { amountUsd = amount },
                secrets = new { },
                enabled = true,
                activate = true
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<Guid> CreateProductAsync(HttpClient adminClient)
    {
        using var response = await adminClient.PostAsJsonAsync(
            "/api/admin/products",
            new ProductRequest(
                "Shipping item",
                $"SHIP-{Guid.NewGuid():N}",
                "Shipping quote test",
                "Shipping",
                10m,
                2,
                0.75m,
                null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<(Guid Id, decimal Shipping)> CreateOrderAsync(HttpClient client, Guid productId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                customerName = "Guest",
                customerEmail = "guest@example.test",
                shippingAddress = "100 Main Street",
                items = new[] { new { productId, quantity = 1 } }
            })
        };
        request.Headers.Add("Idempotency-Key", NewCapability());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (
            document.RootElement.GetProperty("id").GetGuid(),
            document.RootElement.GetProperty("shipping").GetDecimal());
    }

    private static async Task SignInAsync(HttpClient client, string password)
    {
        using var response = await client.PostAsJsonAsync("/auth/login", new { password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            document.RootElement.GetProperty("accessToken").GetString());
    }

    private static string NewCapability() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    private sealed record ProductRequest(
        string Name,
        string Sku,
        string Description,
        string Category,
        decimal Price,
        int Stock,
        decimal? WeightKg,
        string? ImageUrl);
}
