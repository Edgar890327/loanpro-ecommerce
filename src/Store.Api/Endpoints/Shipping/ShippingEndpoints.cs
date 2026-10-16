using Store.Application;
using Store.Api.Orders;

namespace Store.Api.Shipping;

public static class ShippingEndpoints
{
    public static void MapShippingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/shipping/quote", QuoteAsync)
            .AllowAnonymous()
            .WithName("QuoteShipping")
            .WithSummary("Get a public shipping quote for a cart and destination")
            .Produces<ShippingQuoteResponse>()
            .ProducesValidationProblem();
    }

    private static async Task<IResult> QuoteAsync(
        ShippingQuoteRequest request,
        IShippingQuoteService quotes,
        CancellationToken cancellationToken)
    {
        var items = (request.Items ?? [])
            .Select(item => new CartItemInput(item.ProductId, item.Quantity))
            .ToArray();
        var inputErrors = CartInputValidator.Validate(items);
        if (inputErrors.Count > 0)
        {
            return Results.ValidationProblem(inputErrors);
        }

        try
        {
            var quote = await quotes.QuoteAsync(items, request.Destination ?? string.Empty, cancellationToken);
            return Results.Ok(new ShippingQuoteResponse(
                quote.StrategyKey,
                quote.Subtotal,
                quote.Shipping,
                quote.Total));
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["shipping"] = [exception.Message]
            });
        }
        catch (KeyNotFoundException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["items"] = [exception.Message]
            });
        }
    }
}
