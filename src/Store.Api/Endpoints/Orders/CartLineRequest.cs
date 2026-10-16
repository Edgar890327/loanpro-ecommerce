namespace Store.Api.Orders;

public sealed record CartLineRequest(Guid ProductId, int Quantity);
