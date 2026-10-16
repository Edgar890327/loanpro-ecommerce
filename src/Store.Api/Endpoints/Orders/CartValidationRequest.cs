namespace Store.Api.Orders;

public sealed record CartValidationRequest(IReadOnlyList<CartLineRequest>? Items);
