using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Store.Api.Admin;
using Store.Infrastructure;
using Store.Infrastructure.Payments;

namespace Store.IntegrationTests;

public sealed class PaymentEndpointTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task SimulatedPaymentCapturesAndConvertsReservedStockToSale()
    {
        using var factory = new TestApiFactory(fixture);
        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, factory.AdminPassword);
        await SetSimulatedModeAsync(adminClient, SimulatedPaymentOptions.ApproveMode);
        var product = await CreateProductAsync(adminClient, 3);
        using var client = factory.CreateClient();
        await AuthTestClient.RegisterCustomerAsync(client);
        var capability = NewCapability();
        var order = await CreateOrderAsync(client, product.Id, capability);

        using var providerResponse = await client.GetAsync("/api/payments/provider");
        var providerJson = await providerResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, providerResponse.StatusCode);
        Assert.Contains("simulated", providerJson, StringComparison.Ordinal);
        Assert.DoesNotContain("clientSecret", providerJson, StringComparison.OrdinalIgnoreCase);

        using var paymentRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{order.Id}/payment");
        using var paymentResponse = await client.SendAsync(paymentRequest);
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);
        var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentCreated>();
        Assert.NotNull(payment);

        using var captureRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/orders/{order.Id}/payment/capture")
        {
            Content = JsonContent.Create(new { externalReference = payment!.ExternalReference })
        };
        using var captureResponse = await client.SendAsync(captureRequest);
        Assert.Equal(HttpStatusCode.OK, captureResponse.StatusCode);
        var capture = await captureResponse.Content.ReadFromJsonAsync<PaymentCaptured>();
        Assert.Equal("Approved", capture!.PaymentStatus);
        Assert.Equal("Paid", capture.OrderState);

        using var retryRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/orders/{order.Id}/payment/capture")
        {
            Content = JsonContent.Create(new { externalReference = payment.ExternalReference })
        };
        using var retryResponse = await client.SendAsync(retryRequest);
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        var storedProduct = await database.Products.SingleAsync(item => item.Id == product.Id);
        Assert.Equal(2, storedProduct.Stock);
        Assert.Equal(0, storedProduct.Reserved);
        Assert.Equal(1, await database.Payments.CountAsync(item => item.OrderId == order.Id));
        Assert.Contains(await database.StockMovements.Where(item => item.OrderId == order.Id).ToArrayAsync(),
            movement => movement.Type == "Sale" && movement.Quantity == 1);
    }

    [Fact]
    public async Task FailedSimulatedPaymentReleasesReservationAndProviderSettingsRequireAdmin()
    {
        using var factory = new TestApiFactory(fixture);
        using var adminClient = factory.CreateClient();
        using var unauthorized = await adminClient.GetAsync("/api/admin/payment-providers");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        await SignInAsync(adminClient, factory.AdminPassword);
        await SetSimulatedModeAsync(adminClient, SimulatedPaymentOptions.RejectMode);
        var product = await CreateProductAsync(adminClient, 1);
        using var client = factory.CreateClient();
        await AuthTestClient.RegisterCustomerAsync(client);
        var capability = NewCapability();
        var order = await CreateOrderAsync(client, product.Id, capability);

        using var paymentRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{order.Id}/payment");
        using var paymentResponse = await client.SendAsync(paymentRequest);
        var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentCreated>();
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);

        using var captureRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/orders/{order.Id}/payment/capture")
        {
            Content = JsonContent.Create(new { externalReference = payment!.ExternalReference })
        };
        using var captureResponse = await client.SendAsync(captureRequest);
        var capture = await captureResponse.Content.ReadFromJsonAsync<PaymentCaptured>();
        Assert.Equal(HttpStatusCode.OK, captureResponse.StatusCode);
        Assert.Equal("Failed", capture!.PaymentStatus);
        Assert.Equal("Failed", capture.OrderState);

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        Assert.Equal(0, (await database.Products.SingleAsync(item => item.Id == product.Id)).Reserved);
    }

    [Fact]
    public async Task CaptureRejectsAnExpiredReservation()
    {
        using var factory = new TestApiFactory(fixture, reservationTtlSeconds: 2);
        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, factory.AdminPassword);
        await SetSimulatedModeAsync(adminClient, SimulatedPaymentOptions.ApproveMode);
        var product = await CreateProductAsync(adminClient, 1);
        using var client = factory.CreateClient();
        await AuthTestClient.RegisterCustomerAsync(client);
        var capability = NewCapability();
        var order = await CreateOrderAsync(client, product.Id, capability);

        using var paymentRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{order.Id}/payment");
        using var paymentResponse = await client.SendAsync(paymentRequest);
        var payment = await paymentResponse.Content.ReadFromJsonAsync<PaymentCreated>();
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);

        await Task.Delay(TimeSpan.FromSeconds(3));
        using var captureRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/orders/{order.Id}/payment/capture")
        {
            Content = JsonContent.Create(new { externalReference = payment!.ExternalReference })
        };
        using var captureResponse = await client.SendAsync(captureRequest);

        Assert.Equal(HttpStatusCode.Conflict, captureResponse.StatusCode);
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        var storedProduct = await database.Products.SingleAsync(item => item.Id == product.Id);
        var storedOrder = await database.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal("Expired", storedOrder.State.ToString());
        Assert.Equal(0, storedProduct.Reserved);
    }

    [Fact]
    public async Task ProviderSettingsNeverReturnStoredSecrets()
    {
        using var factory = new TestApiFactory(fixture);
        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, factory.AdminPassword);

        using var saveResponse = await adminClient.PutAsJsonAsync(
            "/api/admin/payment-providers/paypal",
            new
            {
                publicConfiguration = new { clientId = "public-client-id", sandbox = "true" },
                secrets = new { clientSecret = "secret-not-for-response" },
                activate = false
            });
        var savedSetting = await saveResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        Assert.DoesNotContain("secret-not-for-response", savedSetting, StringComparison.Ordinal);

        using var listResponse = await adminClient.GetAsync("/api/admin/payment-providers");
        var providerSettings = await listResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.DoesNotContain("secret-not-for-response", providerSettings, StringComparison.Ordinal);
        using var combinedSettingsResponse = await adminClient.GetAsync("/api/admin/settings");
        var combinedSettings = await combinedSettingsResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, combinedSettingsResponse.StatusCode);
        Assert.DoesNotContain("secret-not-for-response", combinedSettings, StringComparison.Ordinal);
        Assert.Contains("\"clientSecret\":\"********\"", combinedSettings, StringComparison.Ordinal);

        using var activateResponse = await adminClient.PutAsJsonAsync(
            "/api/admin/payment-providers/paypal",
            new
            {
                publicConfiguration = new { clientId = "public-client-id", sandbox = "true" },
                secrets = new { clientSecret = "" },
                activate = true
            });
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        using var activeProviderResponse = await adminClient.GetAsync("/api/payments/provider");
        var activeProvider = await activeProviderResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, activeProviderResponse.StatusCode);
        Assert.Contains("\"key\":\"paypal\"", activeProvider, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-not-for-response", activeProvider, StringComparison.Ordinal);
        await SetSimulatedModeAsync(adminClient, SimulatedPaymentOptions.ApproveMode);

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        var setting = await database.PaymentProviderSettings.SingleAsync(item => item.ProviderKey == "paypal");
        Assert.DoesNotContain("secret-not-for-response", setting.ProtectedSecrets, StringComparison.Ordinal);
        Assert.Equal(1, await database.PaymentProviderSettings.CountAsync(item => item.IsActive));
    }

    [Theory]
    [InlineData("simulated")]
    [InlineData("paypal")]
    public async Task PaymentEndpointsOnlyServeTheOwningCustomer(string providerKey)
    {
        using var factory = new TestApiFactory(fixture);
        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, factory.AdminPassword);
        await SetSimulatedModeAsync(adminClient, SimulatedPaymentOptions.ApproveMode);
        if (providerKey == "paypal")
        {
            using var activate = await adminClient.PutAsJsonAsync(
                "/api/admin/payment-providers/paypal",
                new
                {
                    publicConfiguration = new { clientId = "public-client-id", sandbox = "true" },
                    secrets = new { clientSecret = "secret-value" },
                    activate = true
                });
            Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        }

        var product = await CreateProductAsync(adminClient, 2);
        using var owner = factory.CreateClient();
        await AuthTestClient.RegisterCustomerAsync(owner);
        using var stranger = factory.CreateClient();
        await AuthTestClient.RegisterCustomerAsync(stranger);
        using var anonymous = factory.CreateClient();
        var order = await CreateOrderAsync(owner, product.Id, NewCapability());
        var capture = JsonContent.Create(new { externalReference = "any-reference" });

        using var foreignCreate = await stranger.PostAsync($"/api/orders/{order.Id}/payment", null);
        using var foreignCapture = await stranger.PostAsync($"/api/orders/{order.Id}/payment/capture", capture);
        using var adminCreate = await adminClient.PostAsync($"/api/orders/{order.Id}/payment", null);
        using var adminCapture = await adminClient.PostAsync($"/api/orders/{order.Id}/payment/capture", JsonContent.Create(new { externalReference = "any-reference" }));
        using var anonymousCreate = await anonymous.PostAsync($"/api/orders/{order.Id}/payment", null);

        Assert.Equal(HttpStatusCode.NotFound, foreignCreate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignCapture.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminCapture.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCreate.StatusCode);
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        Assert.Equal(0, await database.Payments.CountAsync(item => item.OrderId == order.Id));
    }

    private static async Task SetSimulatedModeAsync(HttpClient adminClient, string mode)
    {
        using var response = await adminClient.PutAsJsonAsync(
            "/api/admin/payment-providers/simulated",
            new
            {
                publicConfiguration = new
                {
                    mode,
                    latencyMilliseconds = "0",
                    rejectCents = "0"
                },
                secrets = new { },
                activate = true
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("clientSecret", content, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ProductResponse> CreateProductAsync(HttpClient client, int stock)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/admin/products",
            new ProductRequest("Payment item", $"PAY-{Guid.NewGuid():N}", "Payment test", "Payments", 25m, stock, 0.5m, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    private static async Task<OrderData> CreateOrderAsync(HttpClient client, Guid productId, string capability)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                shippingAddress = new
                {
                    recipientName = "Buyer",
                    line1 = "1 Test Street",
                    city = "Austin",
                    region = "TX",
                    postalCode = "73301",
                    country = "US"
                },
                items = new[] { new { productId, quantity = 1 } }
            })
        };
        request.Headers.Add("Idempotency-Key", capability);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderData>())!;
    }

    private static Task SignInAsync(HttpClient client, string password) =>
        AuthTestClient.SignInAsync(client, TestApiFactory.AdminEmail, password);

    private static string NewCapability() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");


    private sealed record OrderData(Guid Id, string State);

    private sealed record PaymentCreated(Guid OrderId, string ProviderKey, string ExternalReference);

    private sealed record PaymentCaptured(Guid OrderId, string OrderState, string PaymentStatus);
}
