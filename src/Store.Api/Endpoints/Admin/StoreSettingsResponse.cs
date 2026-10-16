namespace Store.Api.Admin;

public sealed record StoreSettingsResponse(
    IReadOnlyList<PaymentProviderSettingsResponse> PaymentProviders,
    IReadOnlyList<ShippingStrategySettingsResponse> ShippingStrategies);
