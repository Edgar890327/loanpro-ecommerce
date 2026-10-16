using Store.Application;
using Store.Infrastructure.Payments;

namespace Store.UnitTests.Payments;

public sealed class SimulatedPaymentProviderContractTests : PaymentProviderContractTestBase
{
    private readonly SimulatedPaymentProvider _provider = new();

    protected override PaymentProviderConfiguration Configuration => new(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mode"] = SimulatedPaymentOptions.ApproveMode,
            ["latencyMilliseconds"] = "0",
            ["rejectCents"] = "0"
        },
        new Dictionary<string, string>(StringComparer.Ordinal));

    protected override IPaymentProvider CreateProvider() => _provider;
}
