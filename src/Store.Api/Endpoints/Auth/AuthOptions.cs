namespace Store.Api.Auth;

public sealed class AuthOptions
{
    public const string CsrfHeaderName = "X-Requested-With";

    public const string CsrfHeaderValue = "store";

    public string CookieName { get; set; } = "store_session";

    public int RateLimitPerMinute { get; set; } = 10;
}
