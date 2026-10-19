using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Store.Api.Auth;
using Store.Application;

namespace Store.Api.Payments;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/payments/provider", GetActiveProviderAsync)
            .AllowAnonymous()
            .WithName("GetActivePaymentProvider")
            .WithSummary("Get the active payment provider")
            .WithDescription("Returns the active provider descriptor and public checkout configuration only.")
            .Produces<PaymentProviderResponse>()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/orders/{id:guid}/payment", CreateAsync)
            .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
            .WithName("CreateOrderPayment")
            .WithSummary("Create a payment for a reserved order")
            .WithDescription("Requires a customer session that owns the order.")
            .Produces<PaymentCreationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        endpoints.MapPost("/api/orders/{id:guid}/payment/capture", CaptureAsync)
            .RequireAuthorization(AuthorizationPolicies.CustomerOnly)
            .WithName("CaptureOrderPayment")
            .WithSummary("Capture a payment for a reserved order")
            .WithDescription("Captures through the payment provider and converts reserved inventory to sold inventory atomically.")
            .Produces<PaymentCaptureResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway);
    }

    private static async Task<IResult> GetActiveProviderAsync(
        IPaymentProviderRegistry registry,
        CancellationToken cancellationToken)
    {
        var provider = await registry.GetActiveAsync(cancellationToken);
        return provider is null
            ? Results.NotFound()
            : Results.Ok(ToResponse(provider));
    }

    private static async Task<IResult> CreateAsync(
        Guid id,
        ClaimsPrincipal user,
        IPaymentService payments,
        CancellationToken cancellationToken)
    {
        if (user.GetUserId() is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var result = await payments.CreateAsync(id, customerId, cancellationToken);
        return result.Outcome switch
        {
            PaymentOutcome.Created or PaymentOutcome.Existing =>
                Results.Ok(ToResponse(result.Creation!)),
            PaymentOutcome.Expired => Conflict("reservation_expired", "Reservation expired", "The reservation expired before payment could be created."),
            PaymentOutcome.AlreadyFinalized => Conflict("order_not_payable", "Order is no longer payable", "Only a reserved order can be paid."),
            PaymentOutcome.ProviderUnavailable => Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Payment provider unavailable",
                detail: "The payment could not be initialized. Retry the request.",
                extensions: new Dictionary<string, object?> { ["code"] = "payment_provider_unavailable" }),
                        _ => Results.NotFound()
        };
    }

    private static async Task<IResult> CaptureAsync(
        Guid id,
        ClaimsPrincipal user,
        PaymentCaptureRequest request,
        IPaymentService payments,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalReference) || request.ExternalReference.Length > 128)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["payment"] = ["A payment reference is required."]
            });
        }

        if (user.GetUserId() is not { } customerId)
        {
            return Results.Unauthorized();
        }

        var result = await payments.CaptureAsync(
            id,
            customerId,
            request.ExternalReference,
            cancellationToken);
        return result.Outcome switch
        {
            PaymentOutcome.Approved or PaymentOutcome.Failed or PaymentOutcome.Pending =>
                Results.Ok(ToResponse(result.Capture!)),
            PaymentOutcome.Expired => Conflict("reservation_expired", "Reservation expired", "The reservation expired before payment could be captured."),
            PaymentOutcome.AlreadyFinalized => Conflict("order_not_payable", "Order is no longer payable", "Only a reserved order can be captured."),
            PaymentOutcome.ProviderResponseInvalid => Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Invalid payment response",
                detail: "The payment provider response did not match the expected order total and currency.",
                extensions: new Dictionary<string, object?> { ["code"] = "payment_response_invalid" }),
            PaymentOutcome.ProviderUnavailable => Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Payment provider unavailable",
                detail: "The payment could not be captured. Retry the request.",
                extensions: new Dictionary<string, object?> { ["code"] = "payment_provider_unavailable" }),
            PaymentOutcome.NotFound => Results.NotFound(),
            _ => Results.NotFound()
        };
    }

    private static PaymentProviderResponse ToResponse(PaymentProviderView view) => new(
        view.Descriptor.Key,
        view.Descriptor.Name,
        view.Descriptor.ConfigurationFields,
        view.Descriptor.SecretFields,
        view.PublicConfiguration);

    private static PaymentCreationResponse ToResponse(PaymentCreationView view) => new(
        view.OrderId,
        view.ProviderKey,
        view.ExternalReference,
        new PaymentProviderResponse(
            view.Descriptor.Key,
            view.Descriptor.Name,
            view.Descriptor.ConfigurationFields,
            view.Descriptor.SecretFields,
            view.PublicConfiguration));

    private static PaymentCaptureResponse ToResponse(PaymentCaptureView view) =>
        new(view.OrderId, view.OrderState, view.PaymentStatus.ToString());

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
