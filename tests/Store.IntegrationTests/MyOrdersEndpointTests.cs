using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Store.Api.Admin;
using Store.Api.Me;
using Store.Infrastructure;

namespace Store.IntegrationTests;

public sealed class MyOrdersEndpointTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
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

    [Fact]
    public async Task SearchFindsOrdersByNumberProductAndSku()
    {
        using var scenario = await Scenario.StartAsync(fixture);
        var token = Guid.NewGuid().ToString("N")[..8];
        var product = await scenario.CreateProductAsync($"Widget {token}", $"SKU-{token}-A", 50);
        var other = await scenario.CreateProductAsync($"Gadget {token}", $"SKU-{token}-B", 50);
        var customer = await scenario.CreateCustomerAsync();
        var first = await CreateOrderAsync(customer, product.Id);
        var second = await CreateOrderAsync(customer, other.Id);

        var byNumber = await ListAsync(customer, $"q={first.OrderNumber[4..].ToLowerInvariant()}");
        var byPartialNumber = await ListAsync(customer, $"q={second.OrderNumber[^6..]}");
        var byProduct = await ListAsync(customer, $"q=widget {token}");
        var bySku = await ListAsync(customer, $"q=sku-{token}-b");

        Assert.Equal([first.Id], byNumber.Items.Select(item => item.Id));
        Assert.Contains(second.Id, byPartialNumber.Items.Select(item => item.Id));
        Assert.Equal([first.Id], byProduct.Items.Select(item => item.Id));
        Assert.Equal([second.Id], bySku.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task SearchCombinesStatusAndDateFilters()
    {
        using var scenario = await Scenario.StartAsync(fixture);
        var product = await scenario.CreateProductAsync("Combo item", $"COMBO-{Guid.NewGuid():N}", 50);
        var customer = await scenario.CreateCustomerAsync();
        var kept = await CreateOrderAsync(customer, product.Id);
        var cancelled = await CreateOrderAsync(customer, product.Id);
        using var cancel = await customer.PostAsync($"/api/orders/{cancelled.Id}/cancel", null);
        Assert.True(cancel.IsSuccessStatusCode);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var tomorrow = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

        var reserved = await ListAsync(customer, $"status=Reserved&from={today}&to={today}");
        var cancelledOnly = await ListAsync(customer, "status=Cancelled");
        var past = await ListAsync(customer, $"from={DateTime.UtcNow.AddDays(-10):yyyy-MM-dd}&to={yesterday}");
        var future = await ListAsync(customer, $"from={tomorrow}");

        Assert.Equal([kept.Id], reserved.Items.Select(item => item.Id));
        Assert.Equal([cancelled.Id], cancelledOnly.Items.Select(item => item.Id));
        Assert.Empty(past.Items);
        Assert.Empty(future.Items);
    }

    [Fact]
    public async Task PaginationIsStableWithoutDuplicates()
    {
        using var scenario = await Scenario.StartAsync(fixture);
        var product = await scenario.CreateProductAsync("Paged item", $"PAGE-{Guid.NewGuid():N}", 200);
        var customer = await scenario.CreateCustomerAsync();
        var created = new List<Guid>();
        for (var index = 0; index < 27; index++)
        {
            created.Add((await CreateOrderAsync(customer, product.Id)).Id);
        }

        var seen = new List<Guid>();
        var total = 0;
        for (var page = 1; page <= 3; page++)
        {
            var result = await ListAsync(customer, $"page={page}&pageSize=10");
            total = result.TotalCount;
            seen.AddRange(result.Items.Select(item => item.Id));
        }

        Assert.Equal(27, total);
        Assert.Equal(27, seen.Distinct().Count());
        Assert.Equal(created.Order(), seen.Order());
        var descending = await ListAsync(customer, "pageSize=50");
        Assert.True(descending.Items.Zip(descending.Items.Skip(1)).All(pair => pair.First.CreatedAtUtc >= pair.Second.CreatedAtUtc));
    }

    [Fact]
    public async Task CustomersNeverSeeOrOpenEachOthersOrders()
    {
        using var scenario = await Scenario.StartAsync(fixture);
        var product = await scenario.CreateProductAsync("Private item", $"PRIV-{Guid.NewGuid():N}", 20);
        var alice = await scenario.CreateCustomerAsync();
        var bob = await scenario.CreateCustomerAsync();
        var aliceOrder = await CreateOrderAsync(alice, product.Id);

        var bobList = await ListAsync(bob, "q=Private item");
        using var bobDetail = await bob.GetAsync($"/me/orders/{aliceOrder.Id}");
        using var aliceDetail = await alice.GetAsync($"/me/orders/{aliceOrder.Id}");

        Assert.Empty(bobList.Items);
        Assert.Equal(0, bobList.TotalCount);
        Assert.Equal(HttpStatusCode.NotFound, bobDetail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, aliceDetail.StatusCode);
    }

    [Fact]
    public async Task WildcardsAreTreatedAsLiteralText()
    {
        using var scenario = await Scenario.StartAsync(fixture);
        var token = Guid.NewGuid().ToString("N")[..6];
        var percent = await scenario.CreateProductAsync($"Save 50% {token}", $"PCT-{token}", 20);
        var plain = await scenario.CreateProductAsync($"Save 500 {token}", $"PLAIN-{token}", 20);
        var customer = await scenario.CreateCustomerAsync();
        var percentOrder = await CreateOrderAsync(customer, percent.Id);
        await CreateOrderAsync(customer, plain.Id);

        var percentSearch = await ListAsync(customer, $"q={Uri.EscapeDataString("50% " + token)}");
        var underscoreSearch = await ListAsync(customer, $"q={Uri.EscapeDataString("PCT_" + token)}");
        var onlyWildcard = await ListAsync(customer, $"q={Uri.EscapeDataString("%")}");
        var percentProductMatches = onlyWildcard.Items.Select(item => item.Id).ToArray();

        Assert.Equal([percentOrder.Id], percentSearch.Items.Select(item => item.Id));
        Assert.Empty(underscoreSearch.Items);
        Assert.Equal([percentOrder.Id], percentProductMatches);
    }

    [Fact]
    public async Task RenamedOrDeletedProductsDoNotChangeHistory()
    {
        using var scenario = await Scenario.StartAsync(fixture);
        var sku = $"HIST-{Guid.NewGuid():N}";
        var product = await scenario.CreateProductAsync("Original name", sku, 20);
        var customer = await scenario.CreateCustomerAsync();
        var order = await CreateOrderAsync(customer, product.Id);

        var current = (await scenario.Admin.GetFromJsonAsync<ProductResponse>($"/api/admin/products/{product.Id}"))!;
        using var update = await scenario.Admin.PutAsJsonAsync(
            $"/api/admin/products/{product.Id}",
            new ProductRequest("Renamed product", sku, "Changed", "Orders", 99m, 20, 1m, current.Version));
        Assert.True(update.IsSuccessStatusCode);
        using var delete = await scenario.Admin.DeleteAsync($"/api/admin/products/{product.Id}");
        Assert.True(delete.IsSuccessStatusCode);

        var detail = (await customer.GetFromJsonAsync<MyOrderDetailResponse>($"/me/orders/{order.Id}"))!;
        var renamedSearch = await ListAsync(customer, "q=Renamed");
        var originalSearch = await ListAsync(customer, "q=Original name");

        Assert.Equal("Original name", detail.Items.Single().ProductName);
        Assert.Equal(sku, detail.Items.Single().Sku);
        Assert.Equal(25m, detail.Items.Single().UnitPrice);
        Assert.Empty(renamedSearch.Items);
        Assert.Equal([order.Id], originalSearch.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task RequiresACustomerSession()
    {
        using var scenario = await Scenario.StartAsync(fixture);
        using var anonymous = scenario.Factory.CreateClient();

        using var anonymousList = await anonymous.GetAsync("/me/orders");
        using var anonymousDetail = await anonymous.GetAsync($"/me/orders/{Guid.NewGuid()}");
        using var adminList = await scenario.Admin.GetAsync("/me/orders");
        using var adminDetail = await scenario.Admin.GetAsync($"/me/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousList.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousDetail.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminList.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminDetail.StatusCode);
    }

    [Fact]
    public async Task RejectsInvalidQueryValuesAndExposesNoInternalFields()
    {
        using var scenario = await Scenario.StartAsync(fixture);
        var product = await scenario.CreateProductAsync("Shape item", $"SHAPE-{Guid.NewGuid():N}", 20);
        var customer = await scenario.CreateCustomerAsync();
        var order = await CreateOrderAsync(customer, product.Id);

        using var badSort = await customer.GetAsync("/me/orders?sort=price");
        var json = await customer.GetStringAsync($"/me/orders/{order.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, badSort.StatusCode);
        Assert.DoesNotContain("customerId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("idempotency", json, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<MyOrdersPageResponse> ListAsync(HttpClient client, string query) =>
        (await client.GetFromJsonAsync<MyOrdersPageResponse>($"/me/orders?{query}"))!;

    private static async Task<CreatedOrder> CreateOrderAsync(HttpClient client, Guid productId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { shippingAddress = Address, items = new[] { new { productId, quantity = 1 } } })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedOrder>())!;
    }

    private sealed record CreatedOrder(Guid Id, string OrderNumber);

    private sealed class Scenario : IDisposable
    {
        private readonly List<HttpClient> clients = [];

        public required TestApiFactory Factory { get; init; }

        public required HttpClient Admin { get; init; }

        public static async Task<Scenario> StartAsync(PostgreSqlFixture fixture)
        {
            var factory = new TestApiFactory(fixture);
            var admin = factory.CreateClient();
            await AuthTestClient.SignInAsync(admin, TestApiFactory.AdminEmail, factory.AdminPassword);
            return new Scenario { Factory = factory, Admin = admin };
        }

        public async Task<ProductResponse> CreateProductAsync(string name, string sku, int stock)
        {
            using var response = await Admin.PostAsJsonAsync(
                "/api/admin/products",
                new ProductRequest(name, sku, "Orders test", "Orders", 25m, stock, 0.5m, null));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
        }

        public async Task<HttpClient> CreateCustomerAsync()
        {
            var client = Factory.CreateClient();
            await AuthTestClient.RegisterCustomerAsync(client);
            clients.Add(client);
            return client;
        }

        public void Dispose()
        {
            clients.ForEach(client => client.Dispose());
            Admin.Dispose();
            Factory.Dispose();
        }
    }
}
