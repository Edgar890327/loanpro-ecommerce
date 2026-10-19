using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Store.Application;
using Store.Api.Admin;
using Store.Infrastructure;

namespace Store.IntegrationTests;

public sealed class ProductImportEndpointTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Import_RequiresAdministratorToken()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/admin/products/import",
            MakeForm(CsvBytes(""), dryRun: true));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Import_GoldenWorkbookCsvSupportsDryRunReimportAndExactStockTotals()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, factory.AdminPassword);
        var fileBytes = await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "loanpro-products.csv"));
        var fileHash = Convert.ToHexString(SHA256.HashData(fileBytes));
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();

        using (var dryRunResponse = await client.PostAsync(
                   "/api/admin/products/import",
                   MakeForm(fileBytes, dryRun: true)))
        {
            Assert.Equal(HttpStatusCode.OK, dryRunResponse.StatusCode);
            var dryRun = await dryRunResponse.Content.ReadFromJsonAsync<ProductImportReport>();
            Assert.NotNull(dryRun);
            Assert.True(dryRun.DryRun);
            Assert.Equal(87, dryRun.Summary.CreatedProducts);
            Assert.Equal(121737, dryRun.Summary.StockAdded);
            Assert.Equal(6, dryRun.Summary.Warnings);
            Assert.Equal(5, dryRun.Summary.Rejected);
            Assert.Equal(1, dryRun.Summary.ExactDuplicates);
            Assert.Equal(95, dryRun.Rows.Count);
        }

        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            Assert.False(await database.ImportBatches.AnyAsync(batch => batch.FileHash == fileHash));
            Assert.False(await database.Products.IgnoreQueryFilters()
                .AnyAsync(product => product.SkuNormalized == "RS-001"));
            Assert.False(await database.Categories.AnyAsync(category => category.NormalizedName == "FOOTWEAR"));
        }

        using (var importResponse = await client.PostAsync(
                   "/api/admin/products/import",
                   MakeForm(fileBytes, dryRun: false)))
        {
            Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
            var import = await importResponse.Content.ReadFromJsonAsync<ProductImportReport>();
            Assert.NotNull(import);
            Assert.False(import.DryRun);
            Assert.Equal(87, import.Summary.CreatedProducts);
            Assert.Equal(121737, import.Summary.StockAdded);
            Assert.Equal(6, import.Summary.Warnings);
            Assert.Equal(5, import.Summary.Rejected);
            Assert.Equal(1, import.Summary.ExactDuplicates);
        }

        Guid batchId;
        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            var runningShoes = await database.Products.SingleAsync(product => product.SkuNormalized == "RS-001");
            var bluetoothSpeaker = await database.Products.SingleAsync(product => product.SkuNormalized == "BS-021");
            Assert.Equal(270, runningShoes.Stock);
            Assert.Equal(420, bluetoothSpeaker.Stock);
            Assert.Equal("Running Shoes", runningShoes.Name);
            Assert.Equal(89.99m, runningShoes.Price);
            var batch = await database.ImportBatches.SingleAsync(item => item.FileHash == fileHash);
            batchId = batch.Id;
            Assert.Equal(87, batch.CreatedProducts);
            Assert.Equal(121737, batch.StockAdded);
            Assert.Equal(6, batch.WarningCount);
            Assert.Equal(5, batch.RejectedCount);
            Assert.Equal(1, batch.ExactDuplicateCount);
            Assert.True(Guid.TryParse(batch.ImportedBy, out _));
            var movements = await database.StockMovements
                .Where(movement => movement.ImportBatchId == batchId)
                .ToListAsync();
            Assert.Equal(87, movements.Count);
            Assert.All(movements, movement => Assert.Equal("Import", movement.Type));
            Assert.Equal(121737, movements.Sum(movement => (long)movement.Quantity));
        }

        using (var unconfirmed = await client.PostAsync(
                   "/api/admin/products/import",
                   MakeForm(fileBytes, dryRun: false)))
        {
            Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);
        }

        using (var confirmed = await client.PostAsync(
                   "/api/admin/products/import",
                   MakeForm(fileBytes, dryRun: false, confirmReimport: true)))
        {
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
            var report = await confirmed.Content.ReadFromJsonAsync<ProductImportReport>();
            Assert.True(report!.ReimportDetected);
        }

        await using (var database = await contextFactory.CreateDbContextAsync())
        {
            Assert.Equal(540, (await database.Products.SingleAsync(product => product.SkuNormalized == "RS-001")).Stock);
            Assert.Equal(840, (await database.Products.SingleAsync(product => product.SkuNormalized == "BS-021")).Stock);
            Assert.Equal(2, await database.ImportBatches.CountAsync(batch => batch.FileHash == fileHash));
        }
    }

    [Fact]
    public async Task Import_ConcurrentFilesAtomicallyAddStockToSameNormalizedSku()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, factory.AdminPassword);
        var sku = $"CONCURRENT-{Guid.NewGuid():N}";
        var firstFile = CsvBytes($"Concurrent, {sku} ,First description,Concurrent,2.00,7,1");
        var secondFile = CsvBytes($"Other name,{sku.ToLowerInvariant()},Second description, concurrent ,3.00,11,2");

        var firstRequest = client.PostAsync(
            "/api/admin/products/import",
            MakeForm(firstFile, dryRun: false));
        var secondRequest = client.PostAsync(
            "/api/admin/products/import",
            MakeForm(secondFile, dryRun: false));
        using var firstResponse = await firstRequest;
        using var secondResponse = await secondRequest;

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var firstReport = await firstResponse.Content.ReadFromJsonAsync<ProductImportReport>();
        var secondReport = await secondResponse.Content.ReadFromJsonAsync<ProductImportReport>();
        Assert.Equal(1, firstReport!.Summary.CreatedProducts + secondReport!.Summary.CreatedProducts);
        Assert.Equal(18, firstReport.Summary.StockAdded + secondReport.Summary.StockAdded);
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
        await using var database = await contextFactory.CreateDbContextAsync();
        var product = await database.Products.SingleAsync(item => item.SkuNormalized == sku.ToUpperInvariant());
        Assert.Equal(18, product.Stock);
        Assert.True(new[] { "Concurrent", "Other name" }.Contains(product.Name));
        Assert.True(new[] { 2m, 3m }.Contains(product.Price));
        Assert.Equal(2, await database.StockMovements.CountAsync(movement => movement.ProductId == product.Id));
    }

    [Fact]
    public async Task Import_RejectsMissingFileAndInvalidCsvHeaders()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();
        await SignInAsync(client, factory.AdminPassword);

        using var missingFile = new MultipartFormDataContent();
        missingFile.Add(new StringContent("true"), "dryRun");
        using var missingResponse = await client.PostAsync("/api/admin/products/import", missingFile);
        var invalidHeader = MakeForm(
            Encoding.UTF8.GetBytes("sku,name,description,category,price,stock,weight_kg\n"),
            dryRun: true);
        using var badResponse = await client.PostAsync(
            "/api/admin/products/import",
            invalidHeader);

        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badResponse.StatusCode);
    }

    private static Task SignInAsync(HttpClient client, string adminPassword) =>
        AuthTestClient.SignInAsync(client, TestApiFactory.AdminEmail, adminPassword);

    private static MultipartFormDataContent MakeForm(
        byte[] fileBytes,
        bool dryRun,
        bool confirmReimport = false)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(fileBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "products.csv");
        form.Add(new StringContent(dryRun.ToString()), "dryRun");
        form.Add(new StringContent(confirmReimport.ToString()), "confirmReimport");
        return form;
    }

    private static byte[] CsvBytes(string row) =>
        Encoding.UTF8.GetBytes($"name,sku,description,category,price,stock,weight_kg\n{row}\n");

}
