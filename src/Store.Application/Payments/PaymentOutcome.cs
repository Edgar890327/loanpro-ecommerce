namespace Store.Application;

public enum PaymentOutcome
{
    Created,
    Existing,
    Approved,
    Failed,
    Pending,
    NotFound,
    Expired,
    AlreadyFinalized,
    ProviderUnavailable,
    ProviderResponseInvalid
}
