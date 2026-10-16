namespace Store.Application;

public static class ProductCsvImportPlanner
{
    public static ProductCsvImportPlan Build(IReadOnlyList<ProductCsvRow> rows)
    {
        var exactDuplicates = 0;
        var groups = new Dictionary<string, MutableProductCsvGroup>(StringComparer.Ordinal);
        foreach (var row in rows.Where(row => row.IsValid))
        {
            if (!groups.TryGetValue(row.NormalizedSku, out var group))
            {
                groups.Add(row.NormalizedSku, new MutableProductCsvGroup(row));
                continue;
            }

            if (group.Stock > int.MaxValue - row.Stock)
            {
                row.RejectWith("Combined stock for this SKU exceeds the supported maximum.");
                continue;
            }

            group.Stock += row.Stock;
            group.Rows.Add(row);
            if (AreExactCopies(group.First, row))
            {
                exactDuplicates++;
                row.AddWarning($"Exact duplicate of row {group.First.RowNumber}; stock was combined.");
            }
            else
            {
                row.AddWarning($"Duplicate SKU; stock was combined and row {group.First.RowNumber} fields were retained.");
                foreach (var difference in GetDifferences(group.First, row))
                {
                    row.AddWarning(difference);
                }
            }
        }

        return new ProductCsvImportPlan(
            groups.Values
                .Select(group => new ProductCsvGroup(
                    group.First,
                    group.First.NormalizedSku,
                    group.Stock,
                    group.Rows.ToArray()))
                .ToArray(),
            exactDuplicates);
    }

    private static bool AreExactCopies(ProductCsvRow first, ProductCsvRow next) =>
        first.Name == next.Name &&
        first.Sku == next.Sku &&
        first.Description == next.Description &&
        first.Category == next.Category &&
        first.Price == next.Price &&
        first.Stock == next.Stock &&
        first.WeightKg == next.WeightKg;

    private static IEnumerable<string> GetDifferences(ProductCsvRow first, ProductCsvRow next)
    {
        if (!string.Equals(first.Name, next.Name, StringComparison.Ordinal))
        {
            yield return $"Name differs from row {first.RowNumber}; the first value was retained.";
        }

        if (!string.Equals(first.Description, next.Description, StringComparison.Ordinal))
        {
            yield return $"Description differs from row {first.RowNumber}; the first value was retained.";
        }

        if (!string.Equals(first.Category, next.Category, StringComparison.Ordinal))
        {
            yield return $"Category differs from row {first.RowNumber}; the first value was retained.";
        }

        if (first.Price != next.Price)
        {
            yield return $"Price differs from row {first.RowNumber}; the first value was retained.";
        }

        if (first.WeightKg != next.WeightKg)
        {
            yield return $"Weight differs from row {first.RowNumber}; the first value was retained.";
        }
    }

    private sealed class MutableProductCsvGroup(ProductCsvRow first)
    {
        public ProductCsvRow First { get; } = first;

        public List<ProductCsvRow> Rows { get; } = [first];

        public int Stock { get; set; } = first.Stock;
    }
}
