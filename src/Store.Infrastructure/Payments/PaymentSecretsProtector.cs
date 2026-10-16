using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Store.Application;

namespace Store.Infrastructure.Payments;

public sealed class PaymentSecretsProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("LoanPro.Store.PaymentProviderSecrets.v1");

    public string Protect(IReadOnlyDictionary<string, string> secrets) =>
        _protector.Protect(JsonSerializer.Serialize(secrets));

    public IReadOnlyDictionary<string, string> Unprotect(string protectedSecrets) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(_protector.Unprotect(protectedSecrets))
        ?? new Dictionary<string, string>(StringComparer.Ordinal);
}
