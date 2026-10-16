using Store.Application;
using Store.Domain;

namespace Store.UnitTests;

public sealed class ProductValidationTests
{
    [Fact]
    public void Validate_RejectsInvalidRequiredAndNumericFields()
    {
        var errors = ProductValidator.Validate(new ProductInput(
            "   ",
            " ",
            null,
            " ",
            0,
            -1,
            -0.1m));

        Assert.Contains(nameof(ProductInput.Name), errors.Keys);
        Assert.Contains(nameof(ProductInput.Sku), errors.Keys);
        Assert.Contains(nameof(ProductInput.Category), errors.Keys);
        Assert.Contains(nameof(ProductInput.Price), errors.Keys);
        Assert.Contains(nameof(ProductInput.Stock), errors.Keys);
        Assert.Contains(nameof(ProductInput.WeightKg), errors.Keys);
    }

    [Fact]
    public void Validate_RejectsOversizedFieldsAndInvalidPrecision()
    {
        var errors = ProductValidator.Validate(new ProductInput(
            new string('n', ProductValidator.NameMaxLength + 1),
            new string('s', ProductValidator.SkuMaxLength + 1),
            new string('d', ProductValidator.DescriptionMaxLength + 1),
            new string('c', ProductValidator.CategoryMaxLength + 1),
            1.234m,
            0,
            0.0001m));

        Assert.Equal(6, errors.Count);
    }

    [Fact]
    public void Validate_AcceptsTrimmedTextAndOptionalNonNegativeWeight()
    {
        var errors = ProductValidator.Validate(new ProductInput(
            " Product ",
            " sku ",
            " description ",
            " category ",
            1.25m,
            0,
            null));

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(" abc ", "ABC")]
    [InlineData("Sku-12", "SKU-12")]
    [InlineData(" sku ", "SKU")]
    public void SkuNormalizer_TrimsAndIgnoresCase(string input, string expected) =>
        Assert.Equal(expected, SkuNormalizer.Normalize(input));
}
