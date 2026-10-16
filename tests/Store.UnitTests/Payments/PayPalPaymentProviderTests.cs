using System.Net;
using System.Text.Json;
using Store.Application;
using Store.Infrastructure.Payments;
using Microsoft.Extensions.Caching.Memory;

namespace Store.UnitTests.Payments;

public sealed class PayPalPaymentProviderTests : PaymentProviderContractTestBase
{
    private readonly Queue<HttpResponseMessage> _responses = [];
    private readonly HttpClient _client;
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    public PayPalPaymentProviderTests()
    {
        _client = new HttpClient(new PayPalMessageHandler(_ => _responses.Dequeue()));
    }

    protected override PaymentProviderConfiguration Configuration => new(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["clientId"] = "public-client",
            ["sandbox"] = "true"
        },
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["clientSecret"] = "private-client-secret"
        });

    protected override IPaymentProvider CreateProvider()
    {
        _responses.Enqueue(PayPalMessageHandler.Json(HttpStatusCode.OK, """{"access_token":"token-1","expires_in":3600}"""));
        _responses.Enqueue(PayPalMessageHandler.Json(HttpStatusCode.Created, """{"id":"paypal-order-1","status":"CREATED"}"""));
        _responses.Enqueue(PayPalMessageHandler.Json(HttpStatusCode.OK, CapturePayload("27.00", "USD", "COMPLETED")));
        return new PayPalPaymentProvider(_client, _cache);
    }

    [Fact]
    public async Task CaptureReturnsActualAmountForOrderTotalValidation()
    {
        var provider = CreateProviderWithCapture(CapturePayload("26.99", "USD", "COMPLETED"));
        var order = CreateOrder();
        var context = new PaymentProviderContext(OrderCapability, Configuration);
        var created = await provider.CreatePaymentAsync(context, order, CancellationToken.None);

        var capture = await provider.CapturePaymentAsync(
            context,
            order,
            created.ExternalReference,
            CancellationToken.None);

        Assert.Equal(26.99m, capture.Amount);
    }

    [Fact]
    public async Task CaptureReturnsActualCurrencyForOrderCurrencyValidation()
    {
        var provider = CreateProviderWithCapture(CapturePayload("27.00", "EUR", "COMPLETED"));
        var order = CreateOrder();
        var context = new PaymentProviderContext(OrderCapability, Configuration);
        var created = await provider.CreatePaymentAsync(context, order, CancellationToken.None);

        var capture = await provider.CapturePaymentAsync(
            context,
            order,
            created.ExternalReference,
            CancellationToken.None);

        Assert.Equal("EUR", capture.Currency);
    }

    [Fact]
    public async Task CaptureRecognizesAlreadyCapturedOrderAndVerifiesItsAmount()
    {
        var provider = CreateProviderWithAlreadyCaptured(CapturePayload("27.00", "USD", "COMPLETED"));
        var order = CreateOrder();
        var context = new PaymentProviderContext(OrderCapability, Configuration);
        var created = await provider.CreatePaymentAsync(context, order, CancellationToken.None);

        var capture = await provider.CapturePaymentAsync(
            context,
            order,
            created.ExternalReference,
            CancellationToken.None);

        Assert.True(capture.AlreadyCaptured);
        Assert.Equal(PaymentResultStatus.Approved, capture.Status);
        Assert.Equal(order.Total, capture.Amount);
    }

    [Fact]
    public async Task DeclinedCaptureReturnsNormalizedFailedStatus()
    {
        var provider = CreateProviderWithCapture(CapturePayload("27.00", "USD", "DECLINED"));
        var order = CreateOrder();
        var context = new PaymentProviderContext(OrderCapability, Configuration);
        var created = await provider.CreatePaymentAsync(context, order, CancellationToken.None);

        var capture = await provider.CapturePaymentAsync(
            context,
            order,
            created.ExternalReference,
            CancellationToken.None);

        Assert.Equal(PaymentResultStatus.Failed, capture.Status);
        Assert.Equal(order.Total, capture.Amount);
    }

    [Fact]
    public async Task ExpiredOAuthTokenIsRefreshedAndRequestRetried()
    {
        var tokenCalls = 0;
        var createCalls = 0;
        var handler = new PayPalMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/v1/oauth2/token", StringComparison.Ordinal))
            {
                tokenCalls++;
                return PayPalMessageHandler.Json(HttpStatusCode.OK, $$"""{"access_token":"token-{{tokenCalls}}","expires_in":3600}""");
            }

            createCalls++;
            return createCalls == 1
                ? PayPalMessageHandler.Json(HttpStatusCode.Unauthorized, """{"name":"AUTHENTICATION_FAILURE"}""")
                : PayPalMessageHandler.Json(HttpStatusCode.Created, """{"id":"paypal-order-2","status":"CREATED"}""");
        });
        var client = new HttpClient(handler);
        var provider = new PayPalPaymentProvider(client, new MemoryCache(new MemoryCacheOptions()));

        var result = await provider.CreatePaymentAsync(
            new PaymentProviderContext(OrderCapability, Configuration),
            CreateOrder(),
            CancellationToken.None);

        Assert.Equal("paypal-order-2", result.ExternalReference);
        Assert.Equal(2, tokenCalls);
        Assert.Equal(2, createCalls);
    }

    [Fact]
    public async Task RequestsUseStablePayPalIdempotencyIdsWithinHeaderLimit()
    {
        var requestIds = new List<string>();
        var handler = new PayPalMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/v1/oauth2/token", StringComparison.Ordinal))
            {
                return PayPalMessageHandler.Json(HttpStatusCode.OK, """{"access_token":"token-stable","expires_in":3600}""");
            }

            requestIds.Add(request.Headers.GetValues("PayPal-Request-Id").Single());
            return request.RequestUri.AbsolutePath.EndsWith("/capture", StringComparison.Ordinal)
                ? PayPalMessageHandler.Json(HttpStatusCode.OK, CapturePayload("27.00", "USD", "COMPLETED"))
                : PayPalMessageHandler.Json(HttpStatusCode.Created, """{"id":"paypal-order-stable","status":"CREATED"}""");
        });
        var provider = new PayPalPaymentProvider(
            new HttpClient(handler),
            new MemoryCache(new MemoryCacheOptions()));
        var context = new PaymentProviderContext(OrderCapability, Configuration);
        var order = CreateOrder();

        await provider.CreatePaymentAsync(context, order, CancellationToken.None);
        await provider.CreatePaymentAsync(context, order, CancellationToken.None);
        await provider.CapturePaymentAsync(context, order, "paypal-order-stable", CancellationToken.None);

        Assert.Equal(requestIds[0], requestIds[1]);
        Assert.NotEqual(requestIds[0], requestIds[2]);
        Assert.All(requestIds, id => Assert.InRange(id.Length, 1, 38));
    }

    private IPaymentProvider CreateProviderWithCapture(string capturePayload)
    {
        _responses.Clear();
        _responses.Enqueue(PayPalMessageHandler.Json(HttpStatusCode.OK, """{"access_token":"token-2","expires_in":3600}"""));
        _responses.Enqueue(PayPalMessageHandler.Json(HttpStatusCode.Created, """{"id":"paypal-order-3","status":"CREATED"}"""));
        _responses.Enqueue(PayPalMessageHandler.Json(HttpStatusCode.OK, capturePayload));
        return new PayPalPaymentProvider(_client, _cache);
    }

    private IPaymentProvider CreateProviderWithAlreadyCaptured(string capturedOrderPayload)
    {
        _responses.Clear();
        _responses.Enqueue(PayPalMessageHandler.Json(HttpStatusCode.OK, """{"access_token":"token-3","expires_in":3600}"""));
        _responses.Enqueue(PayPalMessageHandler.Json(HttpStatusCode.Created, """{"id":"paypal-order-4","status":"CREATED"}"""));
        _responses.Enqueue(PayPalMessageHandler.Json(
            HttpStatusCode.UnprocessableEntity,
            """{"details":[{"issue":"ORDER_ALREADY_CAPTURED"}]}"""));
        _responses.Enqueue(PayPalMessageHandler.Json(HttpStatusCode.OK, capturedOrderPayload));
        return new PayPalPaymentProvider(_client, _cache);
    }

    private static string CapturePayload(string amount, string currency, string status) =>
        JsonSerializer.Serialize(new
        {
            status = status == "COMPLETED" ? "COMPLETED" : status,
            purchase_units = new[]
            {
                new
                {
                    amount = new { currency_code = currency, value = amount },
                    payments = new
                    {
                        captures = new[]
                        {
                            new
                            {
                                status,
                                amount = new { currency_code = currency, value = amount }
                            }
                        }
                    }
                }
            }
        });
}
