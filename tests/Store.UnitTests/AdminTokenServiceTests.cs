using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Store.Api.Auth;

namespace Store.UnitTests;

public sealed class AdminTokenServiceTests
{
    private static readonly string Secret =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private static readonly DateTimeOffset Now =
        new(2026, 10, 15, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_CreatesShortLivedAdministratorJwtThatValidates()
    {
        var service = CreateService();

        var token = service.Issue();
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token.AccessToken, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "LoanPro.Store",
            ValidateAudience = true,
            ValidAudience = "LoanPro.Store.Admin",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (notBefore, expires, _, _) =>
                expires > Now.UtcDateTime && (!notBefore.HasValue || notBefore <= Now.UtcDateTime)
        }, out var validatedToken);

        Assert.Equal(Now.AddMinutes(10), token.ExpiresAtUtc);
        Assert.IsType<JwtSecurityToken>(validatedToken);
        Assert.Equal("Administrator", principal.FindFirstValue(ClaimTypes.Role));
        Assert.Equal("admin", principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public void PasswordMatches_ComparesMatchingAndDifferentPasswords()
    {
        Assert.True(AdminTokenService.PasswordMatches("admin-password", "admin-password"));
        Assert.False(AdminTokenService.PasswordMatches("wrong-password", "admin-password"));
    }

    private static AdminTokenService CreateService() => new(
        Options.Create(new JwtOptions { Secret = Secret, ExpiryMinutes = 10 }),
        new FixedTimeProvider(Now));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
