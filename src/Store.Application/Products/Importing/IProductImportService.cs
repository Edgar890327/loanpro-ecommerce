namespace Store.Application;

public interface IProductImportService
{
    Task<ProductImportResult> ImportAsync(
        byte[] fileBytes,
        bool dryRun,
        bool confirmReimport,
        Guid importedByUserId,
        CancellationToken cancellationToken);
}
