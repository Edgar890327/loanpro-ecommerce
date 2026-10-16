namespace Store.Application;

public sealed record ShippingStrategyConfiguration(
    IReadOnlyDictionary<string, string> PublicValues,
    IReadOnlyDictionary<string, string> Secrets);
