namespace Store.Api.Auth;

public sealed class JwtOptions
{
    public string Secret { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 15;

    public string Issuer { get; set; } = "LoanPro.Store";

    public string Audience { get; set; } = "LoanPro.Store.Admin";
}
