using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Store.Infrastructure;

namespace Store.UnitTests;

public sealed class InfrastructureRegistrationTests
{
    [Fact]
    public void AddInfrastructure_RegistersTheDatabaseContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=store;Username=store;Password=store-local-only"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<StoreDbContext>());
    }

    [Fact]
    public void AddInfrastructure_ThrowsWhenTheConnectionStringIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddInfrastructure(configuration));

        Assert.Contains("DefaultConnection", exception.Message);
    }
}
