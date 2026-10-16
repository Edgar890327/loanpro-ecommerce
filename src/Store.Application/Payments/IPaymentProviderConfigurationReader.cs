namespace Store.Application;

public interface IPaymentProviderConfigurationReader
{
    Task<PaymentProviderConfiguration> GetConfigurationAsync(
        string providerKey,
        CancellationToken cancellationToken);
}
