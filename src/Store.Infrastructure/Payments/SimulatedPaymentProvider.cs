using Store.Application;

namespace Store.Infrastructure.Payments;

public sealed class SimulatedPaymentProvider : IPaymentProvider
{
    public PaymentProviderDescriptor Descriptor { get; } = new(
        SimulatedPaymentOptions.ProviderKey,
        "Simulated",
        [
            new ConfigurationFieldDescriptor(
                "mode",
                "Payment mode",
                "select",
                true,
                false,
                [
                    SimulatedPaymentOptions.ApproveMode,
                    SimulatedPaymentOptions.RejectMode,
                    SimulatedPaymentOptions.RejectByCentsMode
                ]),
            new ConfigurationFieldDescriptor(
                "latencyMilliseconds",
                "Latency (milliseconds)",
                "number",
                true,
                false,
                Minimum: 0,
                Maximum: 30_000),
            new ConfigurationFieldDescriptor(
                "rejectCents",
                "Reject amount cents",
                "number",
                true,
                false,
                Minimum: 0,
                Maximum: 99)
        ],
        []);

    public Task<PaymentCreationResult> CreatePaymentAsync(
        PaymentProviderContext context,
        PaymentOrder order,
        CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentCreationResult($"sim-{order.Id:N}"));

    public async Task<PaymentCaptureResult> CapturePaymentAsync(
        PaymentProviderContext context,
        PaymentOrder order,
        string externalReference,
        CancellationToken cancellationToken)
    {
        var values = context.Configuration.PublicValues;
        var mode = Required(values, "mode");
        var latency = ParseInteger(values, "latencyMilliseconds");
        var rejectCents = ParseInteger(values, "rejectCents");
        if (mode is not (SimulatedPaymentOptions.ApproveMode or
            SimulatedPaymentOptions.RejectMode or
            SimulatedPaymentOptions.RejectByCentsMode) ||
            latency is < 0 or > 30_000 ||
            rejectCents is < 0 or > 99)
        {
            throw new PaymentProviderException("The simulated payment provider configuration is invalid.");
        }

        if (latency > 0)
        {
            await Task.Delay(latency, cancellationToken);
        }

        var rejected = mode == SimulatedPaymentOptions.RejectMode ||
            mode == SimulatedPaymentOptions.RejectByCentsMode &&
            (int)(order.Total * 100m % 100m) == rejectCents;
        return new PaymentCaptureResult(
            rejected ? PaymentResultStatus.Failed : PaymentResultStatus.Approved,
            order.Total,
            "USD",
            false);
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value)
            ? value
            : throw new PaymentProviderException("The simulated payment provider configuration is incomplete.");

    private static int ParseInteger(IReadOnlyDictionary<string, string> values, string key) =>
        int.TryParse(Required(values, key), out var value)
            ? value
            : throw new PaymentProviderException("The simulated payment provider configuration is invalid.");
}
