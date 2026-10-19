using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Store.Api.Auth;
using Store.Application;
using Store.Domain;
using Store.UnitTests.Identity;

namespace Store.UnitTests;

public sealed class SessionTokenServiceTests
{
    private static readonly string Secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 18, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Customer)]
    public void Issue_CreatesAShortLivedJwtCarryingOnlyTheIdAndRole(UserRole role)
    {
        var profile = new UserProfile(Guid.NewGuid(), "Ana", role, null, "ana@example.com");

        var token = CreateService().Issue(profile);
        var principal = Validate(token.AccessToken, Now.UtcDateTime);

        Assert.Equal(Now.AddMinutes(10), token.ExpiresAtUtc);
        Assert.Equal(role.ToString(), principal.FindFirstValue(ClaimTypes.Role));
        Assert.Equal(profile.Id.ToString("D"), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(2, principal.Claims.Count(claim =>
            claim.Type is ClaimTypes.Role or ClaimTypes.NameIdentifier));
        Assert.DoesNotContain("Ana", token.AccessToken);
    }

    [Fact]
    public void Validation_RejectsAnExpiredToken()
    {
        var token = CreateService().Issue(new UserProfile(Guid.NewGuid(), "Ana", UserRole.Customer, null, "ana@example.com"));

        Assert.Throws<SecurityTokenExpiredException>(
            () => Validate(token.AccessToken, Now.AddMinutes(11).UtcDateTime));
    }

    [Fact]
    public void Validation_RejectsATokenSignedWithAnotherKey()
    {
        var other = new SessionTokenService(
            Options.Create(new JwtOptions { Secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)) }),
            new FixedTimeProvider(Now));
        var token = other.Issue(new UserProfile(Guid.NewGuid(), "Ana", UserRole.Admin, null, "ana@example.com"));

        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(
            () => Validate(token.AccessToken, Now.UtcDateTime));
    }

    private static ClaimsPrincipal Validate(string token, DateTime at) =>
        new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "LoanPro.Store",
            ValidateAudience = true,
            ValidAudience = "LoanPro.Store.Session",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (_, expires, _, _) =>
                expires > at ? true : throw new SecurityTokenExpiredException()
        }, out _);

    private static SessionTokenService CreateService() => new(
        Options.Create(new JwtOptions { Secret = Secret, ExpiryMinutes = 10 }),
        new FixedTimeProvider(Now));
}
