namespace Store.Application;

public static class SecretMasker
{
    public static IReadOnlyDictionary<string, string> Mask(
        IEnumerable<string> configuredSecretKeys) =>
        configuredSecretKeys.ToDictionary(
            key => key,
            _ => "********",
            StringComparer.Ordinal);
}
