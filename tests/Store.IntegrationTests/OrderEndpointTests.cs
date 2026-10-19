using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Store.Application;
using Store.Api.Admin;
using Store.Infrastructure;

namespace Store.IntegrationTests;

public sealed class OrderEndpointTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task ConcurrentOrdersForLastUnit_OnlyOneOrderReservesStock()
    {
        using var factory = new TestApiFactory(fixture);
        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, factory.AdminPassword);
        var product = await CreateProductAsync(adminClient, 1);
        var buyers = new List<HttpClient>();
        for (var index = 0; index < 12; index++)
        {
            buyers.Add(await CreateCustomerClientAsync(factory));
        }

        var attempts = buyers.Select(buyer =>
            CreateOrderAsync(buyer, product.Id, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"))).ToArray();
        var responses = await Task.WhenAll(attempts);

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(11, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        var storedProduct = await database.Products.SingleAsync(item => item.Id == product.Id);
        Assert.Equal(1, storedProduct.Reserved);
        Assert.True(storedProduct.Reserved <= storedProduct.Stock);
        using var catalogResponse = await buyers[0].GetAsync($"/api/catalog/{product.Id}");
        var catalogProduct = await catalogResponse.Content.ReadFromJsonAsync<CatalogProductResponse>();
        Assert.Equal(0, catalogProduct!.Stock);
        foreach (var response in responses)
        {
            response.Dispose();
        }

        buyers.ForEach(buyer => buyer.Dispose());
    }

    [Fact]
    public async Task CartValidationDoesNotReserveAndOrderCreationIsIdempotent()
    {
        using var factory = new TestApiFactory(fixture);
        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, factory.AdminPassword);
        var product = await CreateProductAsync(adminClient, 4);
        using var client = await CreateCustomerClientAsync(factory);
        var line = new { productId = product.Id, quantity = 2 };

        using var validation = await client.PostAsJsonAsync("/api/cart/validate", new { items = new[] { line } });
        Assert.Equal(HttpStatusCode.OK, validation.StatusCode);
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            Assert.Equal(0, (await database.Products.SingleAsync(item => item.Id == product.Id)).Reserved);
        }

        var key = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var attempts = await Task.WhenAll(
            CreateOrderAsync(client, product.Id, key, 2),
            CreateOrderAsync(client, product.Id, key, 2));
        using var first = attempts[0];
        using var retry = attempts[1];
        using var changedPayload = await CreateOrderAsync(client, product.Id, key, 1);
        var firstOrder = await first.Content.ReadFromJsonAsync<OrderPayload>();
        var retryOrder = await retry.Content.ReadFromJsonAsync<OrderPayload>();

        Assert.Contains(first.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK });
        Assert.Contains(retry.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK });
        Assert.NotEqual(first.StatusCode, retry.StatusCode);
        Assert.Equal(firstOrder!.Id, retryOrder!.Id);
        Assert.Equal(HttpStatusCode.Conflict, changedPayload.StatusCode);
        await using var finalDatabase = await contextFactory.CreateDbContextAsync();
        Assert.Equal(2, (await finalDatabase.Products.SingleAsync(item => item.Id == product.Id)).Reserved);
        Assert.Equal(1, await finalDatabase.Orders.CountAsync(order => order.IdempotencyKeyHash != string.Empty &&
            order.Items.Any(item => item.ProductId == product.Id)));
    }

    [Fact]
    public async Task ExpiredOrderReleasesInventoryAndRecordsMovement()
    {
        using var factory = new TestApiFactory(fixture, reservationTtlSeconds: 2);
        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, factory.AdminPassword);
        var product = await CreateProductAsync(adminClient, 1);
        using var client = await CreateCustomerClientAsync(factory);
        var key = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        using var response = await CreateOrderAsync(client, product.Id, key);
        var created = await response.Content.ReadFromJsonAsync<OrderPayload>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        OrderPayload? current = created;
        while (DateTimeOffset.UtcNow < deadline && current?.State == "Reserved")
        {
            await Task.Delay(250);
            using var status = await client.GetAsync($"/api/orders/{created!.Id}");
            current = await status.Content.ReadFromJsonAsync<OrderPayload>();
        }

        Assert.NotNull(current);
        Assert.Equal("Expired", current.State);
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        Assert.Equal(0, (await database.Products.SingleAsync(item => item.Id == product.Id)).Reserved);
        Assert.Contains(await database.StockMovements
            .Where(movement => movement.OrderId == created!.Id)
            .ToListAsync(), movement => movement.Type == "ReservationExpired" && movement.Quantity == 1);
    }

    [Fact]
    public async Task CancelOrderReleasesInventoryAndForeignCustomersCannotSeeOrCancelIt()
    {
        using var factory = new TestApiFactory(fixture);
        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, factory.AdminPassword);
        var product = await CreateProductAsync(adminClient, 2);
        using var client = await CreateCustomerClientAsync(factory);
        using var other = await CreateCustomerClientAsync(factory);
        using var response = await CreateOrderAsync(client, product.Id, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        var created = await response.Content.ReadFromJsonAsync<OrderPayload>();

        using var foreignStatus = await other.GetAsync($"/api/orders/{created!.Id}");
        using var foreignCancel = await other.PostAsync($"/api/orders/{created.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.NotFound, foreignStatus.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignCancel.StatusCode);

        using var cancelled = await client.PostAsync($"/api/orders/{created.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        Assert.Equal(0, (await database.Products.SingleAsync(item => item.Id == product.Id)).Reserved);
    }

    [Fact]
    public async Task CreatingOrdersRequiresACustomerSession()
    {
        using var factory = new TestApiFactory(fixture);
        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, factory.AdminPassword);
        var product = await CreateProductAsync(adminClient, 2);
        using var anonymous = factory.CreateClient();

        using var withoutSession = await CreateOrderAsync(anonymous, product.Id, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        using var asAdmin = await CreateOrderAsync(adminClient, product.Id, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        using var customer = await CreateCustomerClientAsync(factory);
        using var asCustomer = await CreateOrderAsync(customer, product.Id, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.Unauthorized, withoutSession.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Created, asCustomer.StatusCode);
        var order = await asCustomer.Content.ReadFromJsonAsync<OrderPayload>();
        Assert.Matches(@"^ORD-\d{4}-\d{6,}$", order!.OrderNumber);
    }

    [Fact]
    public async Task SameIdempotencyKeyFromTwoCustomersCreatesTwoOrders()
    {
        using var factory = new TestApiFactory(fixture);
        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, factory.AdminPassword);
        var product = await CreateProductAsync(adminClient, 4);
        using var first = await CreateCustomerClientAsync(factory);
        using var second = await CreateCustomerClientAsync(factory);
        var key = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        using var firstResponse = await CreateOrderAsync(first, product.Id, key);
        using var secondResponse = await CreateOrderAsync(second, product.Id, key);
        var firstOrder = await firstResponse.Content.ReadFromJsonAsync<OrderPayload>();
        var secondOrder = await secondResponse.Content.ReadFromJsonAsync<OrderPayload>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.NotEqual(firstOrder!.Id, secondOrder!.Id);
        Assert.NotEqual(firstOrder.OrderNumber, secondOrder.OrderNumber);
    }

    [Fact]
    public async Task FrozenShippingAddressDoesNotChangeWhenTheProfileChanges()
    {
        using var factory = new TestApiFactory(fixture);
        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, factory.AdminPassword);
        var product = await CreateProductAsync(adminClient, 2);
        using var client = await CreateCustomerClientAsync(factory);
        using var response = await CreateOrderAsync(client, product.Id, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        var created = await response.Content.ReadFromJsonAsync<OrderPayload>();
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            var ownerId = await database.Orders.Where(item => item.Id == created!.Id)
                .Select(item => item.CustomerId).SingleAsync();
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Users\" SET \"FullName\" = 'Renamed Person', \"Email\" = 'renamed@example.com' WHERE \"Id\" = {ownerId}");
        }

        using var status = await client.GetAsync($"/api/orders/{created!.Id}");
        var reloaded = await status.Content.ReadFromJsonAsync<OrderPayload>();

        Assert.Equal("Buyer", reloaded!.ShippingAddress!.RecipientName);
        Assert.Equal("100 Test Street", reloaded.ShippingAddress.Line1);
        Assert.NotEqual("renamed@example.com", reloaded.ContactEmail);
    }

    [Fact]
    public async Task FailedOrderReleasesReservedInventory()
    {
        using var factory = new TestApiFactory(fixture);
        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, factory.AdminPassword);
        var product = await CreateProductAsync(adminClient, 1);
        using var client = await CreateCustomerClientAsync(factory);
        var key = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        using var response = await CreateOrderAsync(client, product.Id, key);
        var created = await response.Content.ReadFromJsonAsync<OrderPayload>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var ownerId = await StoredCustomerIdAsync(factory, created!.Id);
        var result = await orders.FailAsync(created.Id, ownerId, CancellationToken.None);

        Assert.Equal(OrderOutcome.Created, result.Outcome);
        Assert.Equal("Failed", result.Order!.State);
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        Assert.Equal(0, (await database.Products.SingleAsync(item => item.Id == product.Id)).Reserved);
    }

    [Fact]
    public async Task ProductUpdateCannotReduceStockBelowActiveReservations()
    {
        using var factory = new TestApiFactory(fixture);
        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, factory.AdminPassword);
        var product = await CreateProductAsync(adminClient, 2);
        using var customerClient = await CreateCustomerClientAsync(factory);
        var key = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        using var order = await CreateOrderAsync(customerClient, product.Id, key, 2);
        Assert.Equal(HttpStatusCode.Created, order.StatusCode);

        using var response = await adminClient.PutAsJsonAsync(
            $"/api/admin/products/{product.Id}",
            new ProductRequest(
                product.Name,
                product.Sku,
                product.Description,
                product.Category,
                product.Price,
                1,
                product.WeightKg,
                product.Version));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        var unchanged = await database.Products.SingleAsync(item => item.Id == product.Id);
        Assert.Equal(2, unchanged.Stock);
        Assert.Equal(2, unchanged.Reserved);
    }

    [Fact]
    public async Task InsufficientCartLineRollsBackEveryEarlierReservation()
    {
        using var factory = new TestApiFactory(fixture);
        using var adminClient = factory.CreateClient();
        await SignInAsync(adminClient, factory.AdminPassword);
        var availableProduct = await CreateProductAsync(adminClient, 3);
        var unavailableProduct = await CreateProductAsync(adminClient, 0);
        using var client = await CreateCustomerClientAsync(factory);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                shippingAddress = Address,
                items = new[]
                {
                    new { productId = availableProduct.Id, quantity = 1 },
                    new { productId = unavailableProduct.Id, quantity = 1 }
                }
            })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        Assert.Equal(0, (await database.Products.SingleAsync(item => item.Id == availableProduct.Id)).Reserved);
        Assert.Equal(0, (await database.Products.SingleAsync(item => item.Id == unavailableProduct.Id)).Reserved);
    }

    private static async Task<ProductResponse> CreateProductAsync(HttpClient client, int stock)
    {
        var sku = $"ORDER-{Guid.NewGuid():N}";
        using var response = await client.PostAsJsonAsync(
            "/api/admin/products",
            new ProductRequest("Order item", sku, "Order test", "Orders", 25m, stock, 0.5m, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    private static async Task<HttpResponseMessage> CreateOrderAsync(
        HttpClient client,
        Guid productId,
        string idempotencyKey,
        int quantity = 1)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                shippingAddress = Address,
                items = new[] { new { productId, quantity } }
            })
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private static Task SignInAsync(HttpClient client, string password) =>
        AuthTestClient.SignInAsync(client, TestApiFactory.AdminEmail, password);


    private static readonly object Address = new
    {
        recipientName = "Buyer",
        line1 = "100 Test Street",
        line2 = (string?)null,
        city = "Austin",
        region = "TX",
        postalCode = "73301",
        country = "US"
    };

    private static async Task<HttpClient> CreateCustomerClientAsync(TestApiFactory factory)
    {
        var client = factory.CreateClient();
        await AuthTestClient.RegisterCustomerAsync(client);
        return client;
    }

    private static async Task<Guid> StoredCustomerIdAsync(TestApiFactory factory, Guid orderId)
    {
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        return await database.Orders.Where(item => item.Id == orderId).Select(item => item.CustomerId).SingleAsync();
    }

    private sealed record AddressPayload(string RecipientName, string Line1);

    private sealed record OrderPayload(
        Guid Id,
        string State,
        string OrderNumber,
        string ContactEmail,
        AddressPayload? ShippingAddress);
}
