using Store.Application;

namespace Store.Api.Admin;

public static class PaymentProviderEndpoints
{
    public static void MapPaymentProviderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/payment-providers").RequireAuthorization("Admin");
        group.MapGet("/", ListAsync)
            .WithName("ListPaymentProviderSettings")
            .WithSummary("List available payment providers and public configuration")
            .WithDescription("Never returns stored provider secrets.")
            .Produces<IReadOnlyList<PaymentProviderSettingsResponse>>();
        group.MapPut("/{providerKey}", SaveAsync)
            .WithName("SavePaymentProviderSettings")
            .WithSummary("Save and optionally activate a payment provider")
            .WithDescription("Provider activation is atomic and does not allow the store to have no active provider.")
            .Produces<PaymentProviderSettingsResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> ListAsync(
        IPaymentProviderRegistry registry,
        CancellationToken cancellationToken)
    {
        var settings = new List<PaymentProviderSettingsResponse>();
        foreach (var descriptor in registry.Descriptors)
        {
            var setting = await registry.GetAsync(descriptor.Key, cancellationToken);
            settings.Add(setting is null
                ? new PaymentProviderSettingsResponse(
                    descriptor.Key,
                    descriptor.Name,
                    descriptor.ConfigurationFields,
                    descriptor.SecretFields,
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    false)
                : ToResponse(setting));
        }

        return Results.Ok(settings);
    }

    private static async Task<IResult> SaveAsync(
        string providerKey,
        PaymentProviderSettingRequest request,
        IPaymentProviderRegistry registry,
        CancellationToken cancellationToken)
    {
        try
        {
            var setting = await registry.SaveAsync(
                providerKey,
                new PaymentProviderSettingInput(
                    request.PublicConfiguration ?? new Dictionary<string, string>(StringComparer.Ordinal),
                    request.Secrets ?? new Dictionary<string, string>(StringComparer.Ordinal),
                    request.Activate),
                cancellationToken);
            return Results.Ok(ToResponse(setting));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["configuration"] = [exception.Message]
            });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Payment provider configuration conflict",
                Detail = exception.Message
            });
        }
    }

    private static PaymentProviderSettingsResponse ToResponse(PaymentProviderView view) => new(
        view.Descriptor.Key,
        view.Descriptor.Name,
        view.Descriptor.ConfigurationFields,
        view.Descriptor.SecretFields,
        view.PublicConfiguration,
        view.IsActive);
}
