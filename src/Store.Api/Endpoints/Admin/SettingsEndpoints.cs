using Store.Application;

namespace Store.Api.Admin;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/settings").RequireAuthorization("Admin");
        group.MapGet("/", GetAsync)
            .WithName("GetStoreSettings")
            .WithSummary("List payment and shipping settings with their schemas")
            .Produces<StoreSettingsResponse>();
        group.MapPut("/shipping/{strategyKey}", SaveShippingAsync)
            .WithName("SaveShippingStrategySettings")
            .WithSummary("Save, enable, or select the active shipping strategy")
            .Produces<ShippingStrategySettingsResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> GetAsync(
        IPaymentProviderRegistry payments,
        IShippingStrategyRegistry shipping,
        CancellationToken cancellationToken)
    {
        var paymentSettings = new List<PaymentProviderSettingsResponse>();
        foreach (var descriptor in payments.Descriptors)
        {
            var view = await payments.GetAsync(descriptor.Key, cancellationToken);
            paymentSettings.Add(view is null
                ? new PaymentProviderSettingsResponse(
                    descriptor.Key,
                    descriptor.Name,
                    descriptor.ConfigurationFields,
                    descriptor.SecretFields,
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    false,
                    false)
                : new PaymentProviderSettingsResponse(
                    view.Descriptor.Key,
                    view.Descriptor.Name,
                    view.Descriptor.ConfigurationFields,
                    view.Descriptor.SecretFields,
                    view.PublicConfiguration,
                    view.MaskedSecrets,
                    view.IsEnabled,
                    view.IsActive));
        }

        var shippingSettings = (await shipping.ListAsync(cancellationToken))
            .Select(view => new ShippingStrategySettingsResponse(
                view.Descriptor.Key,
                view.Descriptor.Name,
                view.Descriptor.ConfigurationFields,
                view.Descriptor.SecretFields,
                view.PublicConfiguration,
                view.MaskedSecrets,
                view.IsEnabled,
                view.IsActive))
            .ToArray();
        return Results.Ok(new StoreSettingsResponse(paymentSettings, shippingSettings));
    }

    private static async Task<IResult> SaveShippingAsync(
        string strategyKey,
        ShippingStrategySettingRequest request,
        IShippingStrategyRegistry registry,
        CancellationToken cancellationToken)
    {
        try
        {
            var view = await registry.SaveAsync(
                strategyKey,
                new ShippingStrategySettingInput(
                    request.PublicConfiguration ?? new Dictionary<string, string>(StringComparer.Ordinal),
                    request.Secrets ?? new Dictionary<string, string>(StringComparer.Ordinal),
                    request.Enabled,
                    request.Activate),
                cancellationToken);
            return Results.Ok(new ShippingStrategySettingsResponse(
                view.Descriptor.Key,
                view.Descriptor.Name,
                view.Descriptor.ConfigurationFields,
                view.Descriptor.SecretFields,
                view.PublicConfiguration,
                view.MaskedSecrets,
                view.IsEnabled,
                view.IsActive));
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
                Title = "Shipping strategy configuration conflict",
                Detail = exception.Message
            });
        }
    }
}
