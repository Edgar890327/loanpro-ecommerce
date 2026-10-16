using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Store.Application;
using Store.Domain;
using Store.Infrastructure;

namespace Store.Api.Admin;

public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/products")
            .RequireAuthorization("Admin");
        group.MapPost("/", CreateAsync);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        return group;
    }

    private static async Task<IResult> CreateAsync(
        ProductRequest request,
        StoreDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var input = request.ToInput();
        var errors = ProductValidator.Validate(input);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var category = await FindOrCreateCategoryAsync(database, request.Category!, cancellationToken);
        var product = new Product(
            request.Name!,
            request.Sku!,
            request.Description ?? string.Empty,
            category,
            request.Price,
            request.Stock,
            request.WeightKg,
            timeProvider.GetUtcNow().UtcDateTime);

        database.Products.Add(product);
        try
        {
            await SaveProductAsync(database, product, category, request, product.UpdatedAtUtc, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueSkuViolation(exception))
        {
            return DuplicateSku();
        }

        await database.Entry(product).ReloadAsync(cancellationToken);
        var version = database.Entry(product).Property<uint>("xmin").CurrentValue;
        return Results.Created($"/api/admin/products/{product.Id}", ProductResponse.From(product, version));
    }

    private static async Task<IResult> ListAsync(
        StoreDbContext database,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pagination"] = ["Page must be positive and pageSize must be between 1 and 100."]
            });
        }

        IQueryable<Product> products = database.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchPattern = $"%{EscapeLike(search.Trim())}%";
            products = products.Where(product =>
                EF.Functions.ILike(product.Name, searchPattern) ||
                EF.Functions.ILike(product.Sku, searchPattern) ||
                EF.Functions.ILike(product.Category.Name, searchPattern));
        }

        var totalCount = await products.CountAsync(cancellationToken);
        var rows = await products
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(product => new ProductResponse(
                product.Id,
                product.Name,
                product.Sku,
                product.Description,
                product.Category.Name,
                product.Price,
                product.Stock,
                product.WeightKg,
                product.IsDeleted,
                product.CreatedAtUtc,
                product.UpdatedAtUtc,
                EF.Property<uint>(product, "xmin")))
            .ToListAsync(cancellationToken);
        return Results.Ok(new PagedResponse<ProductResponse>(rows, page, pageSize, totalCount));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        StoreDbContext database,
        CancellationToken cancellationToken)
    {
        var product = await database.Products
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new ProductResponse(
                item.Id,
                item.Name,
                item.Sku,
                item.Description,
                item.Category.Name,
                item.Price,
                item.Stock,
                item.WeightKg,
                item.IsDeleted,
                item.CreatedAtUtc,
                item.UpdatedAtUtc,
                EF.Property<uint>(item, "xmin")))
            .SingleOrDefaultAsync(cancellationToken);
        return product is null ? Results.NotFound() : Results.Ok(product);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        ProductRequest request,
        StoreDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = ProductValidator.Validate(request.ToInput());
        if (request.Version is null or 0)
        {
            errors = errors.Append(
                new KeyValuePair<string, string[]>(nameof(request.Version), ["A product version is required."]))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var product = await database.Products
            .Include(item => item.Category)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return Results.NotFound();
        }

        database.Entry(product).Property<uint>("xmin").OriginalValue = request.Version!.Value;
        var category = await FindOrCreateCategoryAsync(database, request.Category!, cancellationToken);
        product.Update(
            request.Name!,
            request.Sku!,
            request.Description ?? string.Empty,
            category,
            request.Price,
            request.Stock,
            request.WeightKg,
            timeProvider.GetUtcNow().UtcDateTime);

        try
        {
            await SaveProductAsync(database, product, category, request, product.UpdatedAtUtc, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyConflict();
        }
        catch (DbUpdateException exception) when (IsUniqueSkuViolation(exception))
        {
            return DuplicateSku();
        }

        await database.Entry(product).ReloadAsync(cancellationToken);
        return Results.Ok(ProductResponse.From(
            product,
            database.Entry(product).Property<uint>("xmin").CurrentValue));
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        StoreDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var product = await database.Products.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return Results.NotFound();
        }

        product.Delete(timeProvider.GetUtcNow().UtcDateTime);
        await database.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<Category> FindOrCreateCategoryAsync(
        StoreDbContext database,
        string categoryName,
        CancellationToken cancellationToken)
    {
        var normalizedName = categoryName.Trim().ToUpperInvariant();
        var category = await database.Categories
            .SingleOrDefaultAsync(item => item.NormalizedName == normalizedName, cancellationToken);
        if (category is not null)
        {
            return category;
        }

        category = new Category(categoryName);
        database.Categories.Add(category);
        return category;
    }

    private static async Task SaveProductAsync(
        StoreDbContext database,
        Product product,
        Category category,
        ProductRequest request,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueCategoryViolation(exception))
        {
            database.Entry(category).State = EntityState.Detached;
            var existingCategory = await database.Categories
                .SingleAsync(
                    item => item.NormalizedName == category.NormalizedName,
                    cancellationToken);
            product.Update(
                request.Name!,
                request.Sku!,
                request.Description ?? string.Empty,
                existingCategory,
                request.Price,
                request.Stock,
                request.WeightKg,
                nowUtc);
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool IsUniqueSkuViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Products_SkuNormalized"
        };

    private static bool IsUniqueCategoryViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Categories_NormalizedName"
        };

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static IResult DuplicateSku() => Results.Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = "SKU already exists",
        Detail = "A product with this normalized SKU already exists."
    });

    private static IResult ConcurrencyConflict() => Results.Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = "Product was modified",
        Detail = "This product changed after it was loaded. Refresh it and reapply your changes."
    });
}

public sealed record ProductRequest(
    string? Name,
    string? Sku,
    string? Description,
    string? Category,
    decimal Price,
    int Stock,
    decimal? WeightKg,
    uint? Version)
{
    public ProductInput ToInput() =>
        new(Name, Sku, Description, Category, Price, Stock, WeightKg);
}

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Sku,
    string Description,
    string Category,
    decimal Price,
    int Stock,
    decimal? WeightKg,
    bool IsDeleted,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    uint Version)
{
    public static ProductResponse From(Product product, uint version) => new(
        product.Id,
        product.Name,
        product.Sku,
        product.Description,
        product.Category.Name,
        product.Price,
        product.Stock,
        product.WeightKg,
        product.IsDeleted,
        product.CreatedAtUtc,
        product.UpdatedAtUtc,
        version);
}

public sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int TotalCount);
