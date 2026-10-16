using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Store.Application;
using Store.Infrastructure;
using Store.Infrastructure.Payments;

namespace Store.UnitTests.Payments;

public sealed class PaymentSecretsAndRegistryTests
{
    [Fact]
    public void SecretsAreProtectedAndCanBeRestored()
    {
        var protector = new PaymentSecretsProtector(new EphemeralDataProtectionProvider());
        var secrets = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["clientSecret"] = "secret-value-not-returned"
        };

        var protectedValue = protector.Protect(secrets);

        Assert.DoesNotContain("secret-value-not-returned", protectedValue, StringComparison.Ordinal);
        Assert.Equal("secret-value-not-returned", protector.Unprotect(protectedValue)["clientSecret"]);
    }

    [Fact]
    public void RegistryResolvesProvidersByKeyAndRejectsUnknownKeys()
    {
        var simulated = new SimulatedPaymentProvider();
        var registry = new PaymentProviderRegistry(
            new UnusedContextFactory(),
            [simulated],
            new PaymentSecretsProtector(new EphemeralDataProtectionProvider()),
            TimeProvider.System);

        Assert.Same(simulated, registry.Resolve(SimulatedPaymentOptions.ProviderKey));
        Assert.Throws<KeyNotFoundException>(() => registry.Resolve("unknown"));
    }

    private sealed class UnusedContextFactory : IDbContextFactory<StoreDbContext>
    {
        public StoreDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<StoreDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
                .Options);
    }
}
