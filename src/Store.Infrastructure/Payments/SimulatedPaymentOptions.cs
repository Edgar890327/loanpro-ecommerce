namespace Store.Infrastructure.Payments;

public sealed class SimulatedPaymentOptions
{
    public const string ProviderKey = "simulated";

    public const string ApproveMode = "AlwaysApprove";

    public const string RejectMode = "AlwaysReject";

    public const string RejectByCentsMode = "RejectWhenCents";

    public static IReadOnlyDictionary<string, string> Defaults { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mode"] = ApproveMode,
            ["latencyMilliseconds"] = "0",
            ["rejectCents"] = "99"
        };
}
