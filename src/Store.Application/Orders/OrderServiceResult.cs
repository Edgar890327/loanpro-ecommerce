namespace Store.Application;

public sealed record OrderServiceResult(
    OrderOutcome Outcome,
    OrderView? Order,
    IReadOnlyList<StockIssue> Issues);
