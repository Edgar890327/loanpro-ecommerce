namespace Store.Api.Auth;

public sealed record AdminToken(string AccessToken, DateTimeOffset ExpiresAtUtc);
