using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Store.Application;

namespace Store.Infrastructure.Payments;

public sealed class PayPalPaymentProvider(
    HttpClient httpClient,
    IMemoryCache cache) : IPaymentProvider
{
    private static readonly SemaphoreSlim TokenLock = new(1, 1);

    public PaymentProviderDescriptor Descriptor { get; } = new(
        PayPalPaymentOptions.ProviderKey,
        "PayPal",
        [
            new ConfigurationFieldDescriptor("clientId", "PayPal client ID", "text", true, false),
            new ConfigurationFieldDescriptor("sandbox", "Use sandbox", "boolean", true, false),
            new ConfigurationFieldDescriptor("clientSecret", "PayPal client secret", "password", true, true)
        ],
        ["clientSecret"]);

    public async Task<PaymentCreationResult> CreatePaymentAsync(
        PaymentProviderContext context,
        PaymentOrder order,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{PayPalPaymentOptions.ApiBaseUrl(context.Configuration)}/v2/checkout/orders")
        {
            Content = JsonContent.Create(new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                    new
                    {
                        reference_id = order.Id.ToString(),
                        invoice_id = order.Id.ToString(),
                        custom_id = order.Id.ToString(),
                        amount = new
                        {
                            currency_code = "USD",
                            value = FormatAmount(order.Total),
                            breakdown = new
                            {
                                item_total = new { currency_code = "USD", value = FormatAmount(order.Subtotal) },
                                shipping = new { currency_code = "USD", value = FormatAmount(order.Shipping) }
                            }
                        },
                        items = order.Items.Select(item => new
                        {
                            name = Limit(item.Name, 127),
                            sku = Limit(item.Sku, 127),
                            quantity = item.Quantity.ToString(CultureInfo.InvariantCulture),
                            unit_amount = new { currency_code = "USD", value = FormatAmount(item.UnitPrice) }
                        })
                    }
                }
            })
        };
        request.Headers.Add("PayPal-Request-Id", $"c{order.Id:N}");
        using var response = await SendAuthorizedAsync(request, context.Configuration, cancellationToken);
        using var payload = await ReadPayloadAsync(response, cancellationToken);
        var externalReference = payload.RootElement.TryGetProperty("id", out var id)
            ? id.GetString()
            : null;
        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(externalReference))
        {
            throw new PaymentProviderException("PayPal could not create the payment order.");
        }

        return new PaymentCreationResult(externalReference);
    }

    public async Task<PaymentCaptureResult> CapturePaymentAsync(
        PaymentProviderContext context,
        PaymentOrder order,
        string externalReference,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{PayPalPaymentOptions.ApiBaseUrl(context.Configuration)}/v2/checkout/orders/{Uri.EscapeDataString(externalReference)}/capture")
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Add("PayPal-Request-Id", $"p{order.Id:N}");
        using var response = await SendAuthorizedAsync(request, context.Configuration, cancellationToken);
        using var payload = await ReadPayloadAsync(response, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return ParseCapture(payload.RootElement, alreadyCaptured: false);
        }

        if (IsAlreadyCaptured(payload.RootElement))
        {
            using var existing = await GetOrderAsync(externalReference, context.Configuration, cancellationToken);
            return ParseCapture(existing.RootElement, alreadyCaptured: true);
        }

        throw new PaymentProviderException("PayPal could not capture the payment.");
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpRequestMessage request,
        PaymentProviderConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var contentBytes = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentHeaders = request.Content?.Headers
            .Select(header => new KeyValuePair<string, IEnumerable<string>>(header.Key, header.Value))
            .ToArray() ?? [];
        var token = await GetAccessTokenAsync(configuration, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await SendHttpAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            if (!response.IsSuccessStatusCode)
            {
                return response;
            }

            return response;
        }

        response.Dispose();
        cache.Remove(TokenCacheKey(configuration));
        token = await GetAccessTokenAsync(configuration, cancellationToken);
        using var retry = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Content = contentBytes is null ? null : new ByteArrayContent(contentBytes)
        };
        if (retry.Content is not null)
        {
            foreach (var header in contentHeaders)
            {
                retry.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in request.Headers)
        {
            if (!string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                retry.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await SendHttpAsync(retry, cancellationToken);
    }

    private async Task<string> GetAccessTokenAsync(
        PaymentProviderConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var cacheKey = TokenCacheKey(configuration);
        if (cache.TryGetValue(cacheKey, out string? cached) && !string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        await TokenLock.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(cacheKey, out cached) && !string.IsNullOrWhiteSpace(cached))
            {
                return cached;
            }

            var clientId = Required(configuration.PublicValues, "clientId");
            var clientSecret = Required(configuration.Secrets, "clientSecret");
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{PayPalPaymentOptions.ApiBaseUrl(configuration)}/v1/oauth2/token")
            {
                Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));
            using var response = await SendHttpAsync(request, cancellationToken);
            using var payload = await ReadPayloadAsync(response, cancellationToken);
            if (!response.IsSuccessStatusCode ||
                !payload.RootElement.TryGetProperty("access_token", out var tokenElement) ||
                string.IsNullOrWhiteSpace(tokenElement.GetString()))
            {
                throw new PaymentProviderException("PayPal authentication failed.");
            }

            var token = tokenElement.GetString()!;
            var lifetime = payload.RootElement.TryGetProperty("expires_in", out var expiry)
                ? expiry.GetInt32()
                : 300;
            cache.Set(cacheKey, token, TimeSpan.FromSeconds(Math.Max(30, lifetime - 60)));
            return token;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private async Task<JsonDocument> GetOrderAsync(
        string externalReference,
        PaymentProviderConfiguration configuration,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{PayPalPaymentOptions.ApiBaseUrl(configuration)}/v2/checkout/orders/{Uri.EscapeDataString(externalReference)}");
        using var response = await SendAuthorizedAsync(request, configuration, cancellationToken);
        var payload = await ReadPayloadAsync(response, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            payload.Dispose();
            throw new PaymentProviderException("PayPal could not retrieve the captured order.");
        }

        return payload;
    }

    private static PaymentCaptureResult ParseCapture(
        JsonElement root,
        bool alreadyCaptured)
    {
        var status = root.TryGetProperty("status", out var statusElement) &&
            statusElement.ValueKind == JsonValueKind.String
            ? statusElement.GetString()
            : null;
        var purchaseUnit = root.TryGetProperty("purchase_units", out var units) &&
            units.ValueKind == JsonValueKind.Array && units.GetArrayLength() > 0
            ? units[0]
            : default;
        var capture = purchaseUnit.ValueKind == JsonValueKind.Object &&
            purchaseUnit.TryGetProperty("payments", out var payments) &&
            payments.TryGetProperty("captures", out var captures) &&
            captures.ValueKind == JsonValueKind.Array && captures.GetArrayLength() > 0
            ? captures[0]
            : default;
        var captureStatus = capture.ValueKind == JsonValueKind.Object &&
            capture.TryGetProperty("status", out var captureStatusElement) &&
            captureStatusElement.ValueKind == JsonValueKind.String
            ? captureStatusElement.GetString()
            : null;
        var amount = ReadCaptureAmount(capture, purchaseUnit);
        var currency = ReadCaptureCurrency(capture, purchaseUnit);
        if (status == "COMPLETED" || captureStatus == "COMPLETED")
        {
            return new PaymentCaptureResult(PaymentResultStatus.Approved, amount, currency, alreadyCaptured);
        }

        return captureStatus is "DECLINED" or "FAILED"
            ? new PaymentCaptureResult(PaymentResultStatus.Failed, amount, currency, alreadyCaptured)
            : new PaymentCaptureResult(PaymentResultStatus.Pending, amount, currency, alreadyCaptured);
    }

    private static decimal ReadCaptureAmount(JsonElement capture, JsonElement purchaseUnit)
    {
        if (capture.ValueKind == JsonValueKind.Object &&
            capture.TryGetProperty("amount", out var captureAmount) &&
            captureAmount.ValueKind == JsonValueKind.Object &&
            captureAmount.TryGetProperty("value", out var captureValue) &&
            TryReadAmount(captureValue, out var captureAmountValue))
        {
            return captureAmountValue;
        }

        if (purchaseUnit.ValueKind == JsonValueKind.Object &&
            purchaseUnit.TryGetProperty("amount", out var unitAmount) &&
            unitAmount.ValueKind == JsonValueKind.Object &&
            unitAmount.TryGetProperty("value", out var unitValue) &&
            TryReadAmount(unitValue, out var unitAmountValue))
        {
            return unitAmountValue;
        }

        throw new PaymentProviderException("PayPal returned a capture with an invalid amount.");
    }

    private static string ReadCaptureCurrency(JsonElement capture, JsonElement purchaseUnit)
    {
        if (capture.ValueKind == JsonValueKind.Object &&
            capture.TryGetProperty("amount", out var captureAmount) &&
            captureAmount.ValueKind == JsonValueKind.Object &&
            captureAmount.TryGetProperty("currency_code", out var captureCurrency))
        {
            if (captureCurrency.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(captureCurrency.GetString()))
            {
                return captureCurrency.GetString()!;
            }
        }

        if (purchaseUnit.ValueKind == JsonValueKind.Object &&
            purchaseUnit.TryGetProperty("amount", out var unitAmount) &&
            unitAmount.ValueKind == JsonValueKind.Object &&
            unitAmount.TryGetProperty("currency_code", out var unitCurrency))
        {
            if (unitCurrency.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(unitCurrency.GetString()))
            {
                return unitCurrency.GetString()!;
            }
        }

        throw new PaymentProviderException("PayPal returned a capture with an invalid currency.");
    }

    private static bool TryReadAmount(JsonElement value, out decimal amount)
    {
        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(
                value.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out amount))
        {
            return true;
        }

        amount = default;
        return false;
    }

    private static bool IsAlreadyCaptured(JsonElement root) =>
        root.TryGetProperty("details", out var details) &&
        details.ValueKind == JsonValueKind.Array &&
        details.EnumerateArray().Any(detail =>
            detail.TryGetProperty("issue", out var issue) &&
            issue.ValueKind == JsonValueKind.String &&
            issue.GetString() == "ORDER_ALREADY_CAPTURED");

    private static async Task<JsonDocument> ReadPayloadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new PaymentProviderException("PayPal returned an invalid response.", exception);
        }
    }

    private async Task<HttpResponseMessage> SendHttpAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new PaymentProviderException("PayPal could not be reached.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PaymentProviderException("PayPal request timed out.", exception);
        }
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new PaymentProviderException("PayPal configuration is incomplete.");

    private static string FormatAmount(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Limit(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum];

    private static string TokenCacheKey(PaymentProviderConfiguration configuration)
    {
        var clientId = configuration.PublicValues.GetValueOrDefault("clientId", string.Empty);
        return $"paypal-token-{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{PayPalPaymentOptions.ApiBaseUrl(configuration)}:{clientId}")))}";
    }
}
