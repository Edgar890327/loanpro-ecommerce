namespace Store.Application;

public enum ProductAdminOutcome
{
    Succeeded,
    NotFound,
    DuplicateSku,
    ConcurrencyConflict,
    ReservedStockConflict
}
