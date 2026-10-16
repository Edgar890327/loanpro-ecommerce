namespace Store.Domain;

public sealed class Category
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public ICollection<Product> Products { get; private set; } = new List<Product>();

    private Category()
    {
    }

    public Category(string name)
    {
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
    }
}
