using Store.Domain;

namespace Store.Application;

public sealed class AdminSeeder(
    IUserRepository users,
    IPasswordHasher hasher,
    TimeProvider timeProvider)
{
    public async Task<bool> EnsureAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalized = EmailNormalizer.Normalize(email);
        if (await users.FindByEmailAsync(normalized, cancellationToken) is not null)
        {
            return false;
        }

        var admin = new User(
            email.Trim(),
            normalized,
            hasher.Hash(password),
            "Administrator",
            UserRole.Admin,
            timeProvider.GetUtcNow().UtcDateTime);

        return await users.TryAddAsync(admin, cancellationToken);
    }
}
