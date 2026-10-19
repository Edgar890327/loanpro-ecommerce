using Store.Application;

namespace Store.Api.E2e;

public static class E2eEndpoints
{
    public static void MapE2eEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/e2e/reset", async (
            IE2eDataReset reset,
            CancellationToken cancellationToken) =>
        {
            await reset.ResetAsync(cancellationToken);
            return Results.NoContent();
        })
        .WithName("ResetE2eData")
        .WithSummary("Reset the E2E database to its fixture")
        .ExcludeFromDescription();
    }
}
