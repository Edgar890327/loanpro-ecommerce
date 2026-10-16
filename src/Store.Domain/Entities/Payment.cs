namespace Store.Domain;

public sealed class Payment
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid OrderId { get; private set; }

    public Order Order { get; private set; } = null!;

    public string ProviderKey { get; private set; } = string.Empty;

    public string ExternalReference { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "USD";

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    private Payment()
    {
    }

    public Payment(
        Guid orderId,
        string providerKey,
        string externalReference,
        string status,
        decimal amount,
        DateTime nowUtc)
    {
        OrderId = orderId;
        ProviderKey = providerKey;
        ExternalReference = externalReference;
        Status = status;
        Amount = amount;
        Currency = "USD";
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Update(string externalReference, string status, DateTime nowUtc)
    {
        ExternalReference = externalReference;
        Status = status;
        UpdatedAtUtc = nowUtc;
    }
}
