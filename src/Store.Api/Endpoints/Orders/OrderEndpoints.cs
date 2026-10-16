using Microsoft.AspNetCore.Mvc;
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
            .AllowAnonymous()
            .WithName("CreateGuestOrder")
            .WithSummary("Create a guest order and reserve inventory")
            .WithDescription("Creates an idempotent guest order and reserves all requested inventory atomically.")
            .Produces<OrderResponse>(StatusCodes.Status201Created)
            .Produces<OrderResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        endpoints.MapGet("/api/orders/{id:guid}", GetAsync)
            .AllowAnonymous()
            .WithName("GetOrderStatus")
            .WithSummary("Get public order status")
            .WithDescription("Returns order state and totals without exposing guest contact details.")
            .Produces<OrderResponse>()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/orders/{id:guid}/cancel", CancelAsync)
            .AllowAnonymous()
            .WithName("CancelGuestOrder")
            .WithSummary("Cancel a reserved guest order")
            .WithDescription("Requires the order's original idempotency key as an order-access capability.")
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
        IOrderService orders,
        CancellationToken cancellationToken)
    {
        var input = request.ToInput();
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
            OrderOutcome.AmountOutOfRange => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Order total is outside the supported range",
                detail: "The order total must fit the supported USD amount."),
            OrderOutcome.IdempotencyConflict => Results.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Idempotency key was already used",
                Detail = "Use a new key when submitting a different order."
            }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IOrderService orders,
        CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(id, cancellationToken);
        return order is null
            ? Results.NotFound()
            : Results.Ok(OrderResponse.From(order));
    }

    private static async Task<IResult> CancelAsync(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        IOrderService orders,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length is < 16 or > 200)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["The order's original idempotency key is required."]
            });
        }

        var result = await orders.CancelAsync(id, idempotencyKey, cancellationToken);
        return result.Outcome switch
        {
            OrderOutcome.Created => Results.Ok(OrderResponse.From(result.Order!)),
            OrderOutcome.AlreadyFinalized => Results.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Order can no longer be cancelled",
                Detail = "Only a reserved order can be cancelled."
            }),
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
        return Results.Conflict(problem);
    }
}
