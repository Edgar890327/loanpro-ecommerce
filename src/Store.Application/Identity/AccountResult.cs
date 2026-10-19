namespace Store.Application;

public sealed record AccountResult(
    AccountOutcome Outcome,
    UserProfile? Profile = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);
