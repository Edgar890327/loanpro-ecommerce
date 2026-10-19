using Store.Application;

namespace Store.Infrastructure.Payments;

public static class PayPalPaymentOptions
{
    public const string ProviderKey = "paypal";

    public static string ApiBaseUrl(
        PaymentProviderConfiguration configuration,
        string? overrideUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideUrl))
        {
            if (!Uri.TryCreate(overrideUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https") ||
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new InvalidOperationException("PAYPAL_API_BASE_URL must be an absolute HTTP URL without user information.");
            }

            return overrideUrl.TrimEnd('/');
        }

        var sandbox = configuration.PublicValues.TryGetValue("sandbox", out var value) &&
            bool.TryParse(value, out var enabled) && enabled;
        return sandbox ? "https://api-m.sandbox.paypal.com" : "https://api-m.paypal.com";
    }
}
