using Store.Domain;

namespace Store.UnitTests;

public sealed class ProductEntityTests
{
    [Fact]
    public void Constructor_NormalizesSkuAndSetsProductPropertiesAndTimestamps()
    {
        var now = new DateTime(2026, 10, 15, 18, 0, 0, DateTimeKind.Utc);
        var category = new Category("Kitchen");

        var product = new Product(
            "  Kettle  ",
            "  kettle-01 ",
            "  Stainless steel  ",
            category,
            25.50m,
            4,
            1.2m,
            now);

        Assert.Equal("Kettle", product.Name);
        Assert.Equal("kettle-01", product.Sku);
        Assert.Equal("KETTLE-01", product.SkuNormalized);
        Assert.Equal("Stainless steel", product.Description);
        Assert.Equal(category.Id, product.CategoryId);
        Assert.Same(category, product.Category);
        Assert.Equal(25.50m, product.Price);
        Assert.Equal(4, product.Stock);
        Assert.Equal(1.2m, product.WeightKg);
        Assert.Equal(now, product.CreatedAtUtc);
        Assert.Equal(now, product.UpdatedAtUtc);
        Assert.Equal(0u, product.Version);
        Assert.False(product.IsDeleted);
    }

    [Fact]
    public void UpdateAndDeleteChangeEditableFieldsAndLogicalDeleteState()
    {
        var now = DateTime.UtcNow;
        var product = new Product("Kettle", "K-1", "", new Category("Kitchen"), 1m, 0, null, now);
        var nextUpdate = now.AddMinutes(1);
        var category = new Category("Appliances");

        product.Update("Toaster", "T-2", "Toasts bread", category, 2.50m, 3, 4m, nextUpdate);
        product.Delete(nextUpdate.AddMinutes(1));

        Assert.Equal("Toaster", product.Name);
        Assert.Equal("T-2", product.Sku);
        Assert.Equal("T-2", product.SkuNormalized);
        Assert.Equal(category.Id, product.CategoryId);
        Assert.True(product.IsDeleted);
        Assert.Equal(nextUpdate.AddMinutes(1), product.UpdatedAtUtc);
    }

    [Fact]
    public void Category_TrimsAndNormalizesName()
    {
        var category = new Category("  small appliances ");

        Assert.Equal("small appliances", category.Name);
        Assert.Equal("SMALL APPLIANCES", category.NormalizedName);
    }
}
