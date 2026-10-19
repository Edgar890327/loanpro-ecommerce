using Microsoft.AspNetCore.Mvc;
using Store.Application;

namespace Store.Api.Admin;

public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/products")
            .RequireAuthorization(Store.Api.Auth.AuthorizationPolicies.AdminOnly);
        group.MapPost("/", CreateAsync);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        return group;
    }

    private static async Task<IResult> CreateAsync(
        ProductRequest request,
        IProductAdministration products,
        CancellationToken cancellationToken)
    {
        var errors = ProductValidator.Validate(request.ToInput());
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await products.CreateAsync(request.ToInput(), cancellationToken);
        if (result.Outcome == ProductAdminOutcome.DuplicateSku)
        {
            return DuplicateSku();
        }

        var response = ProductResponse.From(result.Product!);
        return Results.Created($"/api/admin/products/{response.Id}", response);
    }

    private static async Task<IResult> ListAsync(
        IProductAdministration products,
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

        var result = await products.ListAsync(page, pageSize, search, cancellationToken);
        return Results.Ok(new PagedResponse<ProductResponse>(
            result.Items.Select(ProductResponse.From).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IProductAdministration products,
        CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(id, cancellationToken);
        return product is null ? Results.NotFound() : Results.Ok(ProductResponse.From(product));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        ProductRequest request,
        IProductAdministration products,
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

        var result = await products.UpdateAsync(
            id,
            request.ToInput(),
            request.Version!.Value,
            cancellationToken);
        return result.Outcome switch
        {
            ProductAdminOutcome.NotFound => Results.NotFound(),
            ProductAdminOutcome.DuplicateSku => DuplicateSku(),
            ProductAdminOutcome.ConcurrencyConflict => ConcurrencyConflict(),
            ProductAdminOutcome.ReservedStockConflict => ReservedStockConflict(),
            _ => Results.Ok(ProductResponse.From(result.Product!))
        };
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        IProductAdministration products,
        CancellationToken cancellationToken)
    {
        var result = await products.DeleteAsync(id, cancellationToken);
        return result == ProductAdminOutcome.NotFound ? Results.NotFound() : Results.NoContent();
    }

    private static IResult DuplicateSku() => Conflict(
        "duplicate_sku",
        "SKU already exists",
        "A product with this normalized SKU already exists.");

    private static IResult ConcurrencyConflict() => Conflict(
        "product_concurrency_conflict",
        "Product was modified",
        "This product changed after it was loaded. Refresh it and reapply your changes.");

    private static IResult ReservedStockConflict() => Conflict(
        "reserved_stock_conflict",
        "Stock is reserved",
        "Stock cannot be reduced below the quantity currently reserved by active orders.");

    private static IResult Conflict(string code, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = title,
            Detail = detail
        };
        problem.Extensions["code"] = code;
        return Results.Conflict(problem);
    }
}
