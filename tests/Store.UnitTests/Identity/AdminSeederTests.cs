using Store.Application;
using Store.Domain;

namespace Store.UnitTests.Identity;

public sealed class AdminSeederTests
{
    [Fact]
    public async Task Ensure_CreatesTheAdminWhenMissing()
    {
        var users = new InMemoryUserRepository();
        var seeder = new AdminSeeder(users, new FakePasswordHasher(), TimeProvider.System);

        var created = await seeder.EnsureAsync(" Admin@Example.com ", "admin-password", CancellationToken.None);

        Assert.True(created);
        var admin = Assert.Single(users.Users);
        Assert.Equal("admin@example.com", admin.EmailNormalized);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Equal("hash:admin-password", admin.PasswordHash);
    }

    [Fact]
    public async Task Ensure_DoesNotModifyAnExistingAdmin()
    {
        var users = new InMemoryUserRepository();
        var seeder = new AdminSeeder(users, new FakePasswordHasher(), TimeProvider.System);
        await seeder.EnsureAsync("admin@example.com", "first-password", CancellationToken.None);

        var created = await seeder.EnsureAsync("ADMIN@example.com", "second-password", CancellationToken.None);

        Assert.False(created);
        Assert.Equal("hash:first-password", Assert.Single(users.Users).PasswordHash);
    }
}
