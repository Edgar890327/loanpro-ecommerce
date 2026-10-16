namespace Store.Domain;

public sealed class ImportBatch
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string FileHash { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public int RowCount { get; private set; }

    public int CreatedProducts { get; private set; }

    public long StockAdded { get; private set; }

    public int WarningCount { get; private set; }

    public int RejectedCount { get; private set; }

    public int ExactDuplicateCount { get; private set; }

    public string ImportedBy { get; private set; } = string.Empty;

    public ICollection<StockMovement> StockMovements { get; private set; } = new List<StockMovement>();

    private ImportBatch()
    {
    }

    public ImportBatch(
        string fileHash,
        DateTime createdAtUtc,
        int rowCount,
        int createdProducts,
        long stockAdded,
        int warningCount,
        int rejectedCount,
        int exactDuplicateCount,
        string importedBy)
    {
        FileHash = fileHash;
        CreatedAtUtc = createdAtUtc;
        RowCount = rowCount;
        CreatedProducts = createdProducts;
        StockAdded = stockAdded;
        WarningCount = warningCount;
        RejectedCount = rejectedCount;
        ExactDuplicateCount = exactDuplicateCount;
        ImportedBy = importedBy;
    }
}
