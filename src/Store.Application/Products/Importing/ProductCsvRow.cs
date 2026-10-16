using Store.Domain;

namespace Store.Application;

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
