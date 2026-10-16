namespace Store.Application;

public interface IShippingStrategyRegistry
{
    IReadOnlyList<ShippingStrategyDescriptor> Descriptors { get; }

    Task<IReadOnlyList<ShippingStrategyView>> ListAsync(CancellationToken cancellationToken);

    Task<ShippingStrategyView> SaveAsync(
        string strategyKey,
        ShippingStrategySettingInput input,
        CancellationToken cancellationToken);

    Task EnsureInitializedAsync(CancellationToken cancellationToken);

    Task<ShippingCalculation> CalculateAsync(
        ShippingContext context,
        CancellationToken cancellationToken);
}
