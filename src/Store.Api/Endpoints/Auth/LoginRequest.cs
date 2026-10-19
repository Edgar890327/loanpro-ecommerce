namespace Store.Api.Auth;

public sealed record LoginRequest(string? Email, string? Password);
