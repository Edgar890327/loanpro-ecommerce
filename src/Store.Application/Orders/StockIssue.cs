namespace Store.Application;

public sealed record StockIssue(Guid ProductId, int Requested, int Available);
