using Store.Application;
using Store.Domain;

namespace Store.UnitTests;

public sealed class CatalogQueryBuilderTests
{
    [Fact]
    public void Build_NormalizesSearchAndCategoryAndPreservesFilters()
    {
        var query = CatalogQueryBuilder.Build(
            "  Café ",
            " Drinks ",
            2.50m,
            15m,
            true,
            " PRICE ",
            2,
            12);

        Assert.Equal("CAFE", query.Query);
        Assert.Equal("Drinks", query.Category);
        Assert.Equal(2.50m, query.MinPrice);
        Assert.Equal(15m, query.MaxPrice);
        Assert.True(query.InStock);
        Assert.Equal(CatalogSort.Price, query.Sort);
        Assert.Equal(2, query.Page);
        Assert.Equal(12, query.PageSize);
    }

    [Theory]
    [InlineData(null, CatalogSort.Relevance)]
    [InlineData("relevance", CatalogSort.Relevance)]
    [InlineData("price", CatalogSort.Price)]
    [InlineData("name", CatalogSort.Name)]
    public void Build_MapsAllowedSortModes(string? sort, CatalogSort expected) =>
        Assert.Equal(expected, CatalogQueryBuilder.Build(null, null, null, null, null, sort, 1, 12).Sort);

    [Fact]
    public void Build_RejectsUnsupportedSortMode() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CatalogQueryBuilder.Build(null, null, null, null, null, "random", 1, 12));

    [Fact]
    public void ApplySort_PrioritizesExactMatchesThenPrefixesForRelevance()
    {
        var products = new[]
        {
            MakeProduct("Other tea", "OTHER"),
            MakeProduct("Café latte", "OTHER"),
            MakeProduct("Café", "OTHER"),
            MakeProduct("Other", "CAFE-01")
        };
        var query = CatalogQueryBuilder.Build("cafe", null, null, null, null, "relevance", 1, 12);

        Assert.Equal(
            new[] { products[2].Id, products[1].Id, products[3].Id, products[0].Id },
            CatalogQueryBuilder.ApplySort(products.AsQueryable(), query).Select(product => product.Id));
    }

    [Fact]
    public void ApplySort_OrdersPriceAndNameDeterministically()
    {
        var products = new[]
        {
            MakeProduct("C", "C", 2m),
            MakeProduct("B", "B", 1m),
            MakeProduct("A", "A", 1m)
        };
        var priceQuery = CatalogQueryBuilder.Build(null, null, null, null, null, "price", 1, 12);
        var nameQuery = CatalogQueryBuilder.Build(null, null, null, null, null, "name", 1, 12);

        Assert.Equal(
            new[] { products[2].Id, products[1].Id, products[0].Id },
            CatalogQueryBuilder.ApplySort(products.AsQueryable(), priceQuery).Select(product => product.Id));
        Assert.Equal(
            new[] { products[2].Id, products[1].Id, products[0].Id },
            CatalogQueryBuilder.ApplySort(products.AsQueryable(), nameQuery).Select(product => product.Id));
    }

    private static Product MakeProduct(string name, string sku, decimal price = 1m) =>
        new(name, sku, "", new Category($"Test-{Guid.NewGuid():N}"), price, 0, null, DateTime.UtcNow);
}
