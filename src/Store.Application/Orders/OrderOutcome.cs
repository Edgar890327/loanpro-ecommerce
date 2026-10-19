namespace Store.Application;

public enum OrderOutcome
{
    Created,
    Existing,
    OutOfStock,
    AmountOutOfRange,
    IdempotencyConflict,
    NotFound,
    AlreadyFinalized
}
