using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Store.Api.Auth;

public sealed class AdminTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    public AdminToken Issue()
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(settings.ExpiryMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            [
                new Claim(ClaimTypes.NameIdentifier, "admin"),
                new Claim(ClaimTypes.Role, "Administrator")
            ],
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);

        return new AdminToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public static bool PasswordMatches(string supplied, string configured)
    {
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        return CryptographicOperations.FixedTimeEquals(suppliedHash, configuredHash);
    }
}

public sealed record AdminToken(string AccessToken, DateTimeOffset ExpiresAtUtc);
