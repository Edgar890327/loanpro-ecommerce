using Store.Application;

namespace Store.Infrastructure.Payments;

public static class PayPalPaymentOptions
{
    public const string ProviderKey = "paypal";

    public static string ApiBaseUrl(PaymentProviderConfiguration configuration)
    {
        var sandbox = configuration.PublicValues.TryGetValue("sandbox", out var value) &&
            bool.TryParse(value, out var enabled) && enabled;
        return sandbox ? "https://api-m.sandbox.paypal.com" : "https://api-m.paypal.com";
    }
}
