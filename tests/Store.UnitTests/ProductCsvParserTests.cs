using System.Text;
using Store.Application;

namespace Store.UnitTests;

public sealed class ProductCsvParserTests
{
    private readonly ProductCsvParser parser = new();

    [Fact]
    public void Parse_AcceptsUtf8BomEscapedQuotesCommasAndMultilineFields()
    {
        const string csv = "name,sku,description,category,price,stock,weight_kg\r\n" +
                           "\"Coffee, \"\"Classic\"\"\",SKU-1,\"First line,\r\nsecond \"\"quoted\"\" line\",Food,2.50,4,1.2\r\n";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();

        var row = Assert.Single(parser.Parse(new MemoryStream(bytes)).Rows);

        Assert.Equal(2, row.RowNumber);
        Assert.Equal("Coffee, \"Classic\"", row.Name);
        Assert.Equal("First line,\r\nsecond \"quoted\" line", row.Description);
        Assert.Equal("imported", row.Status);
    }

    [Theory]
    [InlineData(",SKU-1,Description,Category,2.50,1,1", "Name is required.")]
    [InlineData("Product,SKU-1,Description,Category,not-a-price,1,1", "Price must be a valid number.")]
    [InlineData("Product,SKU-1,Description,Category,0,1,1", "Price must be greater than zero")]
    [InlineData("Product,SKU-1,Description,Category,2.50,-1,1", "Stock cannot be negative.")]
    [InlineData("Product,SKU-1,Description,Category,2.50,1.5,1", "Stock must be a whole number.")]
    [InlineData("Product,SKU-1,Description,Category,2.50,1,-0.1", "Weight must be non-negative")]
    public void Parse_RejectsInvalidNamePriceStockAndWeight(string rowText, string expectedMessage)
    {
        var row = Assert.Single(ParseRow(rowText));

        Assert.Equal("rejected", row.Status);
        Assert.Contains(row.Messages, message => message.StartsWith(expectedMessage, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_RemovesDollarSymbolWithWarning()
    {
        var row = Assert.Single(ParseRow("Product,SKU-1,Description,Category,$29.99,1,1"));

        Assert.Equal(29.99m, row.Price);
        Assert.Equal("imported_with_warning", row.Status);
        Assert.Contains(row.Messages, message => message.Contains("dollar symbol", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_DefaultsEmptyCategoryAndWeightWithWarnings()
    {
        var row = Assert.Single(ParseRow("Product,SKU-1,Description,,2.50,1,"));

        Assert.Equal("Uncategorized", row.Category);
        Assert.Null(row.WeightKg);
        Assert.Equal("imported_with_warning", row.Status);
        Assert.Equal(2, row.Messages.Count);
    }

    [Theory]
    [InlineData(",SKU-1,Description,Category,2.50,1,1", "Name is required.")]
    [InlineData("Product,,Description,Category,2.50,1,1", "SKU is required.")]
    [InlineData("Product,SKU-1,Description,Category,2.50,1,-0.001", "Weight must be non-negative")]
    public void Parse_RejectsAdditionalRequiredAndPrecisionConstraints(string rowText, string expectedMessage)
    {
        var row = Assert.Single(ParseRow(rowText));

        Assert.Equal("rejected", row.Status);
        Assert.Contains(row.Messages, message => message.StartsWith(expectedMessage, StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_RejectsUnexpectedHeaders()
    {
        const string csv = "sku,name,description,category,price,stock,weight_kg\n";

        Assert.Throws<ProductCsvFormatException>(() => parser.Parse(Bytes(csv)));
    }

    [Fact]
    public void Parse_RejectsFilesAboveTheRowLimit()
    {
        var csv = new StringBuilder("name,sku,description,category,price,stock,weight_kg\n");
        for (var row = 0; row <= ProductCsvParser.MaxDataRows; row++)
        {
            csv.AppendLine("Product,SKU,Description,Category,1,0,");
        }

        Assert.Throws<ProductCsvLimitException>(() => parser.Parse(Bytes(csv.ToString())));
    }

    [Fact]
    public void Planner_CombinesNormalizedSkuStockAndReportsExactAndFieldDifferences()
    {
        const string csv = """
            name,sku,description,category,price,stock,weight_kg
            Widget,SKU-1,Original,Tools,2.50,3,1
            Widget,SKU-1,Original,Tools,2.50,3,1
            Widget, sku-1 ,Changed,Tools,3.00,5,1
            """;
        var rows = parser.Parse(Bytes(csv)).Rows;

        var plan = ProductCsvImportPlanner.Build(rows);
        var group = Assert.Single(plan.Groups);

        Assert.Equal(11, group.Stock);
        Assert.Equal(1, plan.ExactDuplicates);
        Assert.Equal("Widget", group.First.Name);
        Assert.Equal("imported_with_warning", rows[1].Status);
        Assert.Equal("imported_with_warning", rows[2].Status);
        Assert.Contains(rows[2].Messages, message => message.StartsWith("Price differs", StringComparison.Ordinal));
        Assert.Contains(rows[2].Messages, message => message.StartsWith("Description differs", StringComparison.Ordinal));
    }

    [Fact]
    public void Planner_RejectsOnlyTheDuplicateThatWouldOverflowStock()
    {
        const string csv = """
            name,sku,description,category,price,stock,weight_kg
            Widget,SKU-1,Original,Tools,2.50,2147483647,1
            Widget,SKU-1,Original,Tools,2.50,1,1
            """;
        var rows = parser.Parse(Bytes(csv)).Rows;

        var plan = ProductCsvImportPlanner.Build(rows);

        Assert.Equal(int.MaxValue, Assert.Single(plan.Groups).Stock);
        Assert.Equal("imported", rows[0].Status);
        Assert.Equal("rejected", rows[1].Status);
        Assert.Contains(rows[1].Messages, message => message.Contains("exceeds", StringComparison.Ordinal));
    }

    private IReadOnlyList<ProductCsvRow> ParseRow(string row) =>
        parser.Parse(Bytes($"name,sku,description,category,price,stock,weight_kg\n{row}\n")).Rows;

    private static MemoryStream Bytes(string value) => new(Encoding.UTF8.GetBytes(value));
}
