namespace Store.Api.Auth;

public sealed record SessionToken(string AccessToken, DateTimeOffset ExpiresAtUtc);
