namespace Store.Domain;

public sealed class Product
{
    private uint xmin = 0;

    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = string.Empty;

    public string Sku { get; private set; } = string.Empty;

    public string SkuNormalized { get; private set; } = string.Empty;

    public string NameNormalized { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string SearchText { get; private set; } = string.Empty;

    public Guid CategoryId { get; private set; }

    public Category Category { get; private set; } = null!;

    public decimal Price { get; private set; }

    public int Stock { get; private set; }

    public int Reserved { get; private set; }

    public int Available => Stock - Reserved;

    public decimal? WeightKg { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public uint Version => xmin;

    private Product()
    {
    }

    public Product(
        string name,
        string sku,
        string description,
        Category category,
        decimal price,
        int stock,
        decimal? weightKg,
        DateTime nowUtc)
    {
        Id = Guid.NewGuid();
        Category = category;
        CategoryId = category.Id;
        CreatedAtUtc = nowUtc;
        Update(name, sku, description, category, price, stock, weightKg, nowUtc);
    }

    public void Update(
        string name,
        string sku,
        string description,
        Category category,
        decimal price,
        int stock,
        decimal? weightKg,
        DateTime nowUtc)
    {
        if (stock < Reserved)
        {
            throw new InvalidOperationException("Stock cannot be lower than reserved inventory.");
        }

        Name = name.Trim();
        NameNormalized = ProductTextNormalizer.Normalize(Name);
        Sku = sku.Trim();
        SkuNormalized = SkuNormalizer.Normalize(sku);
        Description = description.Trim();
        SearchText = ProductTextNormalizer.BuildSearchText(Name, Sku, Description);
        Category = category;
        CategoryId = category.Id;
        Price = price;
        Stock = stock;
        WeightKg = weightKg;
        UpdatedAtUtc = nowUtc;
    }

    public void Delete(DateTime nowUtc)
    {
        IsDeleted = true;
        UpdatedAtUtc = nowUtc;
    }
}
