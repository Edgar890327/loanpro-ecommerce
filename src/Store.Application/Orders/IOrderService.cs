namespace Store.Application;

public interface IOrderService
{
    Task<CartValidationResult> ValidateCartAsync(
        IReadOnlyList<CartItemInput> items,
        CancellationToken cancellationToken);

    Task<OrderServiceResult> CreateAsync(
        CheckoutInput input,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<OrderView?> GetAsync(Guid orderId, CancellationToken cancellationToken);

    Task<OrderServiceResult> CancelAsync(
        Guid orderId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<OrderServiceResult> FailAsync(
        Guid orderId,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
