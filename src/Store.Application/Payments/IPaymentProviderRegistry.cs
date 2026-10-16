namespace Store.Application;

public interface IPaymentProviderRegistry
{
    IReadOnlyList<PaymentProviderDescriptor> Descriptors { get; }

    Task<PaymentProviderView?> GetActiveAsync(CancellationToken cancellationToken);

    Task<PaymentProviderView?> GetAsync(string providerKey, CancellationToken cancellationToken);

    Task<PaymentProviderView> SaveAsync(
        string providerKey,
        PaymentProviderSettingInput input,
        CancellationToken cancellationToken);

    IPaymentProvider Resolve(string providerKey);

    Task EnsureInitializedAsync(CancellationToken cancellationToken);
}
