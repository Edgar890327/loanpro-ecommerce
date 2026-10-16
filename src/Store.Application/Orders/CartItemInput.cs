namespace Store.Application;

public sealed record CartItemInput(Guid ProductId, int Quantity);
