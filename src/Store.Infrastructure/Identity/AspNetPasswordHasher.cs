using Microsoft.AspNetCore.Identity;
using Store.Application;
using Store.Domain;

namespace Store.Infrastructure.Identity;

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private static readonly User Subject = new(
        "user@store.invalid",
        "user@store.invalid",
        string.Empty,
        "User",
        UserRole.Customer,
        DateTime.UnixEpoch);

    private readonly PasswordHasher<User> _hasher = new();

    public AspNetPasswordHasher()
    {
        DummyHash = Hash("dummy-password-for-timing-equalization");
    }

    public string DummyHash { get; }

    public string Hash(string password) => _hasher.HashPassword(Subject, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(Subject, passwordHash, password) != PasswordVerificationResult.Failed;
}
