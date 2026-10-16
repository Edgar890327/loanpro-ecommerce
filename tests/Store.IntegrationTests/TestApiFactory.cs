using Microsoft.AspNetCore.Mvc.Testing;
using System.Security.Cryptography;

namespace Store.IntegrationTests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _originalEnvironment = new(StringComparer.Ordinal);

    public TestApiFactory(PostgreSqlFixture fixture, int? reservationTtlSeconds = null)
    {
        AdminPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        SetEnvironment("ConnectionStrings__DefaultConnection", fixture.ConnectionString);
        SetEnvironment("ADMIN_PASSWORD", AdminPassword);
        SetEnvironment("JWT_SECRET", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        SetEnvironment("JWT_EXPIRY_MINUTES", "15");
        SetEnvironment("ERROR_LOG_RETENTION_DAYS", "30");
        SetEnvironment("Shipping__FlatRateUsd", "0");
        SetEnvironment("RESERVATION_TTL_SECONDS", (reservationTtlSeconds ?? 900).ToString());
        SetEnvironment("RESERVATION_SCAN_INTERVAL_SECONDS", reservationTtlSeconds.HasValue ? "1" : "15");
    }

    public string AdminPassword { get; }

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
