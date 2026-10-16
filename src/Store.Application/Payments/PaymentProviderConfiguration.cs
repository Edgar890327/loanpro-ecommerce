namespace Store.Application;

public sealed record PaymentProviderConfiguration(
    IReadOnlyDictionary<string, string> PublicValues,
    IReadOnlyDictionary<string, string> Secrets);
