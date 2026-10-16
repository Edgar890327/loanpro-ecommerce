using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Store.Api;
using Store.Application;
using Store.Domain;
using Store.Infrastructure;

namespace Store.IntegrationTests;

public sealed class PublicCatalogEndpointTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Catalog_CombinesSearchFiltersAndStablePaginationAndHidesDeletedProducts()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var categoryName = $"Drinks-{suffix}";
        var otherCategoryName = $"Kitchen-{suffix}";
        var category = new Category(categoryName);
        var otherCategory = new Category(otherCategoryName);
        var products = new[]
        {
            MakeProduct($"Café, \"Classic\" {suffix}", $"CAFE-{suffix}-1", "Ground beans", category, 10m, 5),
            MakeProduct($"Cafe <script>alert(\"x\")</script> {suffix}", $"CAFE-{suffix}-2", "Mild coffee", category, 11m, 1),
            MakeProduct($"Rare tea {suffix}", $"TEA-{suffix}", "Tea", category, 12m, 0),
            MakeProduct($"Other mug {suffix}", $"MUG-{suffix}", "Ceramic", otherCategory, 12m, 2),
            MakeProduct($"Café special {suffix}", $"CAFE-{suffix}-3", "Ground beans", category, 13m, 3),
            MakeProduct($"Café {suffix}", $"EXACT-{suffix}", "Exact match", category, 14m, 4),
            MakeProduct($"Café {suffix} deluxe", $"PREFIX-{suffix}", "Prefix match", category, 15m, 4)
        };

        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            database.Products.AddRange(products);
            await database.SaveChangesAsync();
        }

        var categoryFilter = Uri.EscapeDataString(categoryName);
        var firstPage = await client.GetFromJsonAsync<CatalogPage>(
            $"/api/catalog?q=cafe&category={categoryFilter}&minPrice=10&maxPrice=13&inStock=true&sort=price&page=1&pageSize=1");
        var secondPage = await client.GetFromJsonAsync<CatalogPage>(
            $"/api/catalog?q=cafe&category={categoryFilter}&minPrice=10&maxPrice=13&inStock=true&sort=price&page=2&pageSize=1");
        var thirdPage = await client.GetFromJsonAsync<CatalogPage>(
            $"/api/catalog?q=cafe&category={categoryFilter}&minPrice=10&maxPrice=13&inStock=true&sort=price&page=3&pageSize=1");

        Assert.Equal(3, firstPage!.TotalCount);
        Assert.Equal(3, secondPage!.TotalCount);
        var first = Assert.Single(firstPage.Items);
        var second = Assert.Single(secondPage.Items);
        var third = Assert.Single(thirdPage!.Items);
        Assert.Equal(products[0].Id, first.Id);
        Assert.Equal(products[1].Id, second.Id);
        Assert.Equal(products[4].Id, third.Id);
        Assert.Equal(3, new[] { first.Id, second.Id, third.Id }.Distinct().Count());

        var relevanceQuery = Uri.EscapeDataString($"Café {suffix}");
        var relevanceResults = await client.GetFromJsonAsync<CatalogPage>(
            $"/api/catalog?q={relevanceQuery}&sort=relevance&page=1&pageSize=10");
        Assert.Equal(
            new[] { products[5].Id, products[6].Id },
            relevanceResults!.Items.Select(product => product.Id));

        var exactSpecialQuery = Uri.EscapeDataString($"Café, \"Classic\" {suffix}");
        var specialMatch = await client.GetFromJsonAsync<CatalogPage>(
            $"/api/catalog?q={exactSpecialQuery}&page=1&pageSize=10");
        Assert.Contains(specialMatch!.Items, product => product.Id == products[0].Id);

        var scriptQuery = Uri.EscapeDataString("<script>alert(\"x\")</script>");
        var scriptMatch = await client.GetFromJsonAsync<CatalogPage>(
            $"/api/catalog?q={scriptQuery}&page=1&pageSize=10");
        Assert.Contains(scriptMatch!.Items, product => product.Id == products[1].Id);

        var injectionQuery = Uri.EscapeDataString($"' OR 1=1 -- {suffix}");
        var injectionAttempt = await client.GetFromJsonAsync<CatalogPage>(
            $"/api/catalog?q={injectionQuery}&page=1&pageSize=10");
        Assert.Empty(injectionAttempt!.Items);

        var categories = await client.GetFromJsonAsync<CatalogCategoryResponse[]>("/api/catalog/categories");
        Assert.Contains(categories!, item => item.Name == categoryName && item.ProductCount == 6);

        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            var deleted = await database.Products.SingleAsync(product => product.Id == products[2].Id);
            deleted.Delete(DateTime.UtcNow);
            await database.SaveChangesAsync();
        }

        var remainingProducts = await client.GetFromJsonAsync<CatalogPage>(
            $"/api/catalog?category={categoryFilter}&page=1&pageSize=10");
        Assert.DoesNotContain(remainingProducts!.Items, product => product.Id == products[2].Id);

        var countsAfterDelete = await client.GetFromJsonAsync<CatalogCategoryResponse[]>("/api/catalog/categories");
        Assert.Contains(countsAfterDelete!, item => item.Name == categoryName && item.ProductCount == 5);

        using var deletedDetail = await client.GetAsync($"/api/catalog/{products[2].Id}");
        Assert.Equal(HttpStatusCode.NotFound, deletedDetail.StatusCode);
    }

    [Fact]
    public async Task Catalog_DetailReturnsSpecialNamesAndWeightAsPlainJsonText()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();
        var product = MakeProduct(
            $"<script>alert('safe')</script> {Guid.NewGuid():N}",
            $"HTML-{Guid.NewGuid():N}",
            "Description with <b>literal</b> markup",
            new Category($"Markup-{Guid.NewGuid():N}"),
            7m,
            2);
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            database.Products.Add(product);
            await database.SaveChangesAsync();
        }

        var detail = await client.GetFromJsonAsync<CatalogProductResponse>(
            $"/api/catalog/{product.Id}");

        Assert.NotNull(detail);
        Assert.Equal(product.Name, detail.Name);
        Assert.Equal(product.Description, detail.Description);
        Assert.Equal(1.25m, detail.WeightKg);
    }

    [Fact]
    public async Task Catalog_RejectsInvalidSortAndPriceRange()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        using var invalidSort = await client.GetAsync("/api/catalog?sort=quantity");
        using var invalidRange = await client.GetAsync("/api/catalog?minPrice=20&maxPrice=10");

        Assert.Equal(HttpStatusCode.BadRequest, invalidSort.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRange.StatusCode);
    }

    private static Product MakeProduct(
        string name,
        string sku,
        string description,
        Category category,
        decimal price,
        int stock) =>
        new(
            name,
            sku,
            description,
            category,
            price,
            stock,
            1.25m,
            DateTime.UtcNow);
}
