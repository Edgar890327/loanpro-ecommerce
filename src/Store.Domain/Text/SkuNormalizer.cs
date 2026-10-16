namespace Store.Domain;

public static class SkuNormalizer
{
    public static string Normalize(string sku) => sku.Trim().ToUpperInvariant();
}
