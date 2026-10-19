using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Store.Application;

namespace Store.Api.Auth;

public sealed class SessionTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    public SessionToken Issue(UserProfile profile)
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
                new Claim(ClaimTypes.NameIdentifier, profile.Id.ToString("D")),
                new Claim(ClaimTypes.Role, profile.Role.ToString())
            ],
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);

        return new SessionToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
