namespace Store.Application;

public sealed record ProductAdminResult(ProductAdminOutcome Outcome, ProductAdminView? Product);
