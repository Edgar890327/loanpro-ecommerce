namespace Store.Application;

public sealed record RegisterInput(string? Email, string? Password, string? FullName, string? PreferredLocale);
