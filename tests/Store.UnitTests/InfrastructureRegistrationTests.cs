using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Store.Application;
using Store.Infrastructure;
using Store.Infrastructure.ErrorLogging;

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

    [Fact]
    public void AddInfrastructure_RegistersErrorLoggingAndAppliesRetentionOverride()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=store;Username=store;Password=test",
                ["ErrorLogging:BatchSize"] = "2",
                ["ErrorLogging:QueueCapacity"] = "4",
                ["ERROR_LOG_RETENTION_DAYS"] = "45"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var errorLogOptions = provider.GetRequiredService<IOptions<ErrorLogOptions>>().Value;

        Assert.Equal(2, errorLogOptions.BatchSize);
        Assert.Equal(4, errorLogOptions.QueueCapacity);
        Assert.Equal(45, errorLogOptions.RetentionDays);
        Assert.IsAssignableFrom<IDbContextFactory<StoreDbContext>>(
            provider.GetRequiredService<IDbContextFactory<StoreDbContext>>());
        Assert.IsAssignableFrom<IErrorLogWriter>(
            provider.GetRequiredService<IErrorLogWriter>());
    }

    [Theory]
    [InlineData("ErrorLogging:BatchSize", "0")]
    [InlineData("ErrorLogging:QueueCapacity", "0")]
    [InlineData("ERROR_LOG_RETENTION_DAYS", "0")]
    [InlineData("ErrorLogging:BatchInterval", "00:00:00")]
    [InlineData("ErrorLogging:RetentionInterval", "00:00:00")]
    public void AddInfrastructure_RejectsInvalidErrorLoggingOptions(string key, string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=store;Username=store;Password=test",
                [key] = value
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ErrorLogOptions>>().Value);
    }
}
