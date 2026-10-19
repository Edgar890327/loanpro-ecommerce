using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Store.Api.Auth;
using Store.Application;

namespace Store.Api.Orders;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/cart/validate", ValidateCartAsync)
            .AllowAnonymous()
            .WithName("ValidateCart")
            .WithSummary("Validate cart inventory without reserving stock")
            .WithDescription("Returns current availability and USD totals without changing inventory.")
            .Produces<CartValidationResult>()
            .ProducesValidationProblem();

        endpoints.MapPost("/api/orders", CreateAsync)
            .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
            .WithName("CreateOrder")
            .WithSummary("Create an order for the signed-in customer and reserve inventory")
            .WithDescription("Creates an order idempotent per customer and key, and reserves all requested inventory atomically.")
            .Produces<OrderResponse>(StatusCodes.Status201Created)
            .Produces<OrderResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        endpoints.MapGet("/api/orders/{id:guid}", GetAsync)
            .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
            .WithName("GetOrderStatus")
            .WithSummary("Get one of the signed-in customer's orders")
            .WithDescription("Returns the order only to its owner; any other order responds 404.")
            .Produces<OrderResponse>()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/orders/{id:guid}/cancel", CancelAsync)
            .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
            .WithName("CancelOrder")
            .WithSummary("Cancel a reserved order of the signed-in customer")
            .WithDescription("Releases the reservation. Orders of other customers respond 404.")
            .Produces<OrderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> ValidateCartAsync(
        CartValidationRequest request,
        IOrderService orders,
        CancellationToken cancellationToken)
    {
        var input = ToInputs(request.Items ?? []);
        var errors = CartInputValidator.Validate(input);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        return Results.Ok(await orders.ValidateCartAsync(input, cancellationToken));
    }

    private static async Task<IResult> CreateAsync(
        CheckoutRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        ClaimsPrincipal user,
        IOrderService orders,
        CancellationToken cancellationToken)
    {
        if (user.GetUserId() is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var input = request.ToInput(customerId);
        var errors = CheckoutValidator.Validate(input).ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length is < 16 or > 200)
        {
            errors["Idempotency-Key"] = ["A key between 16 and 200 characters is required."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await orders.CreateAsync(input, idempotencyKey!, cancellationToken);
        return result.Outcome switch
        {
            OrderOutcome.Created => Results.Created(
                $"/api/orders/{result.Order!.Id}",
                OrderResponse.From(result.Order)),
            OrderOutcome.Existing => Results.Ok(OrderResponse.From(result.Order!)),
            OrderOutcome.OutOfStock => StockConflict(result.Issues),
            OrderOutcome.NotFound => Results.Unauthorized(),
            OrderOutcome.AmountOutOfRange => Problem(
                StatusCodes.Status400BadRequest,
                "order_amount_out_of_range",
                "Order total is outside the supported range",
                "The order total must fit the supported USD amount."),
            OrderOutcome.IdempotencyConflict => Conflict(
                "idempotency_conflict",
                "Idempotency key was already used",
                "Use a new key when submitting a different order."),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ClaimsPrincipal user,
        IOrderService orders,
        CancellationToken cancellationToken)
    {
        if (user.GetUserId() is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var order = await orders.GetAsync(id, customerId, cancellationToken);
        return order is null
            ? Results.NotFound()
            : Results.Ok(OrderResponse.From(order));
    }

    private static async Task<IResult> CancelAsync(
        Guid id,
        ClaimsPrincipal user,
        IOrderService orders,
        CancellationToken cancellationToken)
    {
        if (user.GetUserId() is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var result = await orders.CancelAsync(id, customerId, cancellationToken);
        return result.Outcome switch
        {
            OrderOutcome.Created => Results.Ok(OrderResponse.From(result.Order!)),
            OrderOutcome.AlreadyFinalized => Conflict(
                "order_not_cancellable",
                "Order can no longer be cancelled",
                "Only a reserved order can be cancelled."),
            _ => Results.NotFound()
        };
    }

    private static IReadOnlyList<CartItemInput> ToInputs(IReadOnlyList<CartLineRequest> items) =>
        items.Select(item => new CartItemInput(item.ProductId, item.Quantity)).ToArray();

    private static IResult StockConflict(IReadOnlyList<StockIssue> issues)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Inventory changed",
            Detail = "One or more products no longer have enough available inventory."
        };
        problem.Extensions["items"] = issues;
        problem.Extensions["code"] = "inventory_changed";
        return Results.Conflict(problem);
    }

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

    private static IResult Problem(int status, string code, string title, string detail)
    {
        return Results.Problem(
            statusCode: status,
            title: title,
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}
