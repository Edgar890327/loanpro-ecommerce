namespace Store.Application;

public enum PaymentOutcome
{
    Created,
    Existing,
    Approved,
    Failed,
    Pending,
    NotFound,
    InvalidCapability,
    Expired,
    AlreadyFinalized,
    ProviderUnavailable,
    ProviderResponseInvalid
}
