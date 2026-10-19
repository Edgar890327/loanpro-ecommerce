using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace Store.IntegrationTests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _originalEnvironment = new(StringComparer.Ordinal);
    private readonly bool _e2eMode;

    public TestApiFactory(
        PostgreSqlFixture fixture,
        int? reservationTtlSeconds = null,
        bool e2eMode = false,
        int authRateLimitPerMinute = 1000)
    {
        _e2eMode = e2eMode;
        SetEnvironment("ConnectionStrings__DefaultConnection", fixture.ConnectionString);
        SetEnvironment("ADMIN_EMAIL", AdminEmail);
        SetEnvironment("ADMIN_PASSWORD", AdminPassword);
        SetEnvironment("AUTH_RATE_LIMIT_PER_MINUTE", authRateLimitPerMinute.ToString());
        SetEnvironment("JWT_SECRET", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        SetEnvironment("JWT_EXPIRY_MINUTES", "30");
        SetEnvironment("ERROR_LOG_RETENTION_DAYS", "30");
        SetEnvironment("Shipping__FlatRateUsd", "0");
        SetEnvironment("RESERVATION_TTL_SECONDS", (reservationTtlSeconds ?? 900).ToString());
        SetEnvironment("RESERVATION_SCAN_INTERVAL_SECONDS", reservationTtlSeconds.HasValue ? "1" : "15");
    }

    public const string AdminEmail = "admin@store.test";

    private static readonly string SharedAdminPassword =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public string AdminPassword => SharedAdminPassword;

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add("X-Requested-With", "store");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["E2E_MODE"] = _e2eMode ? "true" : "false"
            }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        foreach (var (key, value) in _originalEnvironment)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private void SetEnvironment(string key, string value)
    {
        _originalEnvironment.TryAdd(key, Environment.GetEnvironmentVariable(key));
        Environment.SetEnvironmentVariable(key, value);
    }
}
