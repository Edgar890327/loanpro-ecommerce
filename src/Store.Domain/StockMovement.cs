namespace Store.Domain;

public sealed class StockMovement
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public Guid ImportBatchId { get; private set; }

    public ImportBatch ImportBatch { get; private set; } = null!;

    public string Type { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private StockMovement()
    {
    }

    public StockMovement(Guid productId, Guid importBatchId, int quantity, DateTime createdAtUtc)
    {
        ProductId = productId;
        ImportBatchId = importBatchId;
        Type = "Import";
        Quantity = quantity;
        CreatedAtUtc = createdAtUtc;
    }
}
