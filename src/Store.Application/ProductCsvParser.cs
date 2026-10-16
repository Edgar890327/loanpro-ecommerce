using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Store.Domain;

namespace Store.Application;

public sealed class ProductCsvParser
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;
    public const int MaxDataRows = 10_000;
    private static readonly string[] ExpectedHeaders =
        ["name", "sku", "description", "category", "price", "stock", "weight_kg"];

    public ProductCsvParseResult Parse(Stream stream)
    {
        using var textReader = new StreamReader(
            stream,
            new System.Text.UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);
        using var csv = new CsvReader(textReader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            DetectColumnCountChanges = true
        });

        try
        {
            if (!csv.Read())
            {
                throw new ProductCsvFormatException("The CSV file is empty.");
            }

            csv.ReadHeader();
            if (csv.HeaderRecord is null ||
                !csv.HeaderRecord.SequenceEqual(ExpectedHeaders, StringComparer.Ordinal))
            {
                throw new ProductCsvFormatException(
                    "CSV columns must be: name, sku, description, category, price, stock, weight_kg.");
            }

            var rows = new List<ProductCsvRow>();
            var rowNumber = 1;
            while (csv.Read())
            {
                if (rows.Count == MaxDataRows)
                {
                    throw new ProductCsvLimitException($"CSV files cannot contain more than {MaxDataRows} data rows.");
                }

                rowNumber++;
                var row = ParseRow(csv, rowNumber);
                rows.Add(row);
            }

            return new ProductCsvParseResult(rows);
        }
        catch (CsvHelperException exception)
        {
            throw new ProductCsvFormatException("The CSV file contains a malformed record.", exception);
        }
        catch (DecoderFallbackException exception)
        {
            throw new ProductCsvFormatException("The CSV file must use valid UTF-8 encoding.", exception);
        }
    }

    private static ProductCsvRow ParseRow(CsvReader csv, int rowNumber)
    {
        var name = csv.GetField("name") ?? string.Empty;
        var sku = csv.GetField("sku") ?? string.Empty;
        var description = csv.GetField("description") ?? string.Empty;
        var category = csv.GetField("category") ?? string.Empty;
        var rawPrice = csv.GetField("price") ?? string.Empty;
        var rawStock = csv.GetField("stock") ?? string.Empty;
        var rawWeight = csv.GetField("weight_kg") ?? string.Empty;
        var row = new ProductCsvRow(rowNumber, name, sku, description, category);

        var priceText = rawPrice.Trim();
        if (priceText.Contains('$'))
        {
            priceText = priceText.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
            row.AddWarning("The dollar symbol was removed from price.");
        }

        var priceValid = decimal.TryParse(
            priceText,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var price);
        if (!priceValid)
        {
            row.Reject("Price must be a valid number.");
        }

        var stockValid = int.TryParse(
            rawStock,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var stock);
        if (!stockValid)
        {
            row.Reject("Stock must be a whole number.");
        }

        decimal? weight = null;
        if (string.IsNullOrWhiteSpace(rawWeight))
        {
            row.AddWarning("Weight is empty; it was imported as null.");
        }
        else if (decimal.TryParse(
                     rawWeight,
                     NumberStyles.Number,
                     CultureInfo.InvariantCulture,
                     out var parsedWeight))
        {
            weight = parsedWeight;
        }
        else
        {
            row.Reject("Weight must be a valid number.");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            category = "Uncategorized";
            row.Category = category;
            row.AddWarning("Category is empty; Uncategorized was used.");
        }

        if (priceValid)
        {
            row.Price = price;
        }

        if (stockValid)
        {
            row.Stock = stock;
        }

        row.WeightKg = weight;
        var validation = ProductValidator.Validate(
            new ProductInput(name, sku, description, category, row.Price, row.Stock, weight));
        foreach (var message in validation.Values.SelectMany(messages => messages))
        {
            row.Reject(message);
        }

        return row;
    }
}

public sealed class ProductCsvFormatException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class ProductCsvLimitException(string message) : Exception(message);

public sealed record ProductCsvParseResult(IReadOnlyList<ProductCsvRow> Rows);

public sealed class ProductCsvRow(
    int rowNumber,
    string name,
    string sku,
    string description,
    string category)
{
    private readonly List<string> messages = [];

    public int RowNumber { get; } = rowNumber;

    public string Name { get; } = name;

    public string Sku { get; } = sku;

    public string Description { get; } = description;

    public string Category { get; set; } = category;

    public decimal Price { get; set; }

    public int Stock { get; set; }

    public decimal? WeightKg { get; set; }

    public string Status { get; private set; } = "imported";

    public IReadOnlyList<string> Messages => messages;

    public bool IsValid => Status != "rejected";

    public string NormalizedSku => SkuNormalizer.Normalize(Sku);

    public void AddWarning(string message)
    {
        if (IsValid)
        {
            messages.Add(message);
            Status = "imported_with_warning";
        }
    }

    public void Reject(string message)
    {
        messages.Add(message);
        Status = "rejected";
    }

    public void RejectWith(string message)
    {
        if (IsValid)
        {
            Reject(message);
        }
    }
}

public sealed record ProductImportSummary(
    int CreatedProducts,
    long StockAdded,
    int Warnings,
    int Rejected,
    int ExactDuplicates);

public sealed record ProductImportRowReport(int RowNumber, string Status, IReadOnlyList<string> Messages);

public sealed record ProductImportReport(
    string FileHash,
    bool DryRun,
    bool ReimportDetected,
    DateTime? PreviousImportAtUtc,
    ProductImportSummary Summary,
    IReadOnlyList<ProductImportRowReport> Rows);

public sealed record ProductImportResult(
    ProductImportReport Report,
    bool ReimportConfirmationRequired);
