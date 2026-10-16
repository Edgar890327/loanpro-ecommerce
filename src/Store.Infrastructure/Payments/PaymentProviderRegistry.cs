using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Store.Application;
using Store.Domain;

namespace Store.Infrastructure.Payments;

public sealed class PaymentProviderRegistry : IPaymentProviderRegistry, IPaymentProviderConfigurationReader
{
    private readonly IDbContextFactory<StoreDbContext> _contextFactory;
    private readonly IReadOnlyDictionary<string, IPaymentProvider> _providers;
    private readonly PaymentSecretsProtector _secretsProtector;
    private readonly TimeProvider _timeProvider;

    public PaymentProviderRegistry(
        IDbContextFactory<StoreDbContext> contextFactory,
        IEnumerable<IPaymentProvider> providers,
        PaymentSecretsProtector secretsProtector,
        TimeProvider timeProvider)
    {
        _contextFactory = contextFactory;
        _secretsProtector = secretsProtector;
        _timeProvider = timeProvider;
        var providerArray = providers.ToArray();
        if (providerArray.Select(provider => provider.Descriptor.Key).Distinct(StringComparer.Ordinal).Count() !=
            providerArray.Length)
        {
            throw new InvalidOperationException("Payment provider keys must be unique.");
        }

        _providers = providerArray.ToDictionary(
            provider => provider.Descriptor.Key,
            StringComparer.Ordinal);
        Descriptors = providerArray.Select(provider => provider.Descriptor).OrderBy(item => item.Name).ToArray();
    }

    public IReadOnlyList<PaymentProviderDescriptor> Descriptors { get; }

    public IPaymentProvider Resolve(string providerKey) =>
        _providers.TryGetValue(providerKey, out var provider)
            ? provider
            : throw new KeyNotFoundException($"Payment provider '{providerKey}' is not registered.");

    public async Task<PaymentProviderView?> GetActiveAsync(CancellationToken cancellationToken)
    {
        await using var database = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var setting = await database.PaymentProviderSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.IsActive, cancellationToken);
        return setting is null ? null : ToView(setting);
    }

    public async Task<PaymentProviderView?> GetAsync(
        string providerKey,
        CancellationToken cancellationToken)
    {
        _ = Resolve(providerKey);
        await using var database = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var setting = await database.PaymentProviderSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.ProviderKey == providerKey, cancellationToken);
        return setting is null ? null : ToView(setting);
    }

    public async Task<PaymentProviderView> SaveAsync(
        string providerKey,
        PaymentProviderSettingInput input,
        CancellationToken cancellationToken)
    {
        var provider = Resolve(providerKey);
        ValidateConfiguration(provider.Descriptor, input.PublicConfiguration, input.Secrets);
        await using var database = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AcquireSettingsLockAsync(database, cancellationToken);
        var settings = await database.PaymentProviderSettings.ToListAsync(cancellationToken);
        var existing = settings.SingleOrDefault(item => item.ProviderKey == providerKey);
        var currentSecrets = existing is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(
                _secretsProtector.Unprotect(existing.ProtectedSecrets),
                StringComparer.Ordinal);
        foreach (var secretField in provider.Descriptor.SecretFields)
        {
            if (input.Secrets.TryGetValue(secretField, out var secret) && !string.IsNullOrEmpty(secret))
            {
                currentSecrets[secretField] = secret;
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var protectedSecrets = _secretsProtector.Protect(currentSecrets);
        var publicConfigurationJson = JsonSerializer.Serialize(input.PublicConfiguration);
        if (existing is null)
        {
            existing = new PaymentProviderSetting(
                providerKey,
                publicConfigurationJson,
                protectedSecrets,
                input.Activate,
                now);
            database.PaymentProviderSettings.Add(existing);
        }
        else
        {
            existing.Update(publicConfigurationJson, protectedSecrets, now);
        }

        if (input.Activate)
        {
            await database.Database.ExecuteSqlRawAsync(
                "UPDATE \"PaymentProviderSettings\" SET \"IsActive\" = FALSE WHERE \"IsActive\" = TRUE",
                cancellationToken);
            foreach (var setting in settings.Where(item => item.IsActive))
            {
                setting.SetActive(false, now);
            }

            if (database.Entry(existing).State != EntityState.Added)
            {
                database.Entry(existing).Property(item => item.IsActive).OriginalValue = false;
            }

            existing.SetActive(true, now);
        }

        if (input.Activate && providerKey == "paypal" &&
            (!currentSecrets.TryGetValue("clientSecret", out var clientSecret) ||
             string.IsNullOrWhiteSpace(clientSecret)))
        {
            throw new ArgumentException("A PayPal client secret is required before activating PayPal.");
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(existing);
    }

    public async Task<PaymentProviderConfiguration> GetConfigurationAsync(
        string providerKey,
        CancellationToken cancellationToken)
    {
        await using var database = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var setting = await database.PaymentProviderSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.ProviderKey == providerKey, cancellationToken)
            ?? throw new KeyNotFoundException("Payment provider configuration was not found.");
        return new PaymentProviderConfiguration(
            JsonSerializer.Deserialize<Dictionary<string, string>>(setting.PublicConfigurationJson)
                ?? new Dictionary<string, string>(StringComparer.Ordinal),
            _secretsProtector.Unprotect(setting.ProtectedSecrets));
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        await using var database = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AcquireSettingsLockAsync(database, cancellationToken);
        if (!await database.PaymentProviderSettings.AnyAsync(cancellationToken))
        {
            var provider = Resolve(SimulatedPaymentOptions.ProviderKey);
            var defaults = SimulatedPaymentOptions.Defaults;
            database.PaymentProviderSettings.Add(new PaymentProviderSetting(
                provider.Descriptor.Key,
                JsonSerializer.Serialize(defaults),
                _secretsProtector.Protect(new Dictionary<string, string>(StringComparer.Ordinal)),
                true,
                _timeProvider.GetUtcNow().UtcDateTime));
            await database.SaveChangesAsync(cancellationToken);
        }

        var activeCount = await database.PaymentProviderSettings.CountAsync(item => item.IsActive, cancellationToken);
        if (activeCount != 1)
        {
            throw new InvalidOperationException("Exactly one payment provider must be active.");
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private PaymentProviderView ToView(PaymentProviderSetting setting)
    {
        var publicValues = JsonSerializer.Deserialize<Dictionary<string, string>>(setting.PublicConfigurationJson)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        return new PaymentProviderView(
            Resolve(setting.ProviderKey).Descriptor,
            publicValues,
            setting.IsActive);
    }

    private static async Task AcquireSettingsLockAsync(
        StoreDbContext database,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(721044383715)",
            (NpgsqlConnection)database.Database.GetDbConnection(),
            (NpgsqlTransaction?)database.Database.CurrentTransaction?.GetDbTransaction());
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static void ValidateConfiguration(
        PaymentProviderDescriptor descriptor,
        IReadOnlyDictionary<string, string> publicValues,
        IReadOnlyDictionary<string, string> secrets)
    {
        var publicKeys = descriptor.ConfigurationFields
            .Where(field => !field.Secret)
            .Select(field => field.Key)
            .ToHashSet(StringComparer.Ordinal);
        var secretKeys = descriptor.SecretFields.ToHashSet(StringComparer.Ordinal);
        if (publicValues.Keys.Any(key => !publicKeys.Contains(key)) ||
            secrets.Keys.Any(key => !secretKeys.Contains(key)))
        {
            throw new ArgumentException("Payment provider configuration contains unsupported fields.");
        }

        foreach (var field in descriptor.ConfigurationFields.Where(field => !field.Secret))
        {
            if (!publicValues.TryGetValue(field.Key, out var value))
            {
                if (field.Required)
                {
                    throw new ArgumentException($"Configuration field '{field.Key}' is required.");
                }

                foreach (var secretField in descriptor.SecretFields)
                {
                    if (secrets.TryGetValue(secretField, out var secret) && secret.Length > 4000)
                    {
                        throw new ArgumentException($"Secret field '{secretField}' is too long.");
                    }
                }

                continue;
            }

            if (value is null)
            {
                throw new ArgumentException($"Configuration field '{field.Key}' cannot be null.");
            }

            if (field.Required && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Configuration field '{field.Key}' is required.");
            }

            if (field.Type == "boolean" && !bool.TryParse(value, out _))
            {
                throw new ArgumentException($"Configuration field '{field.Key}' must be true or false.");
            }

            if (field.Type == "number" &&
                !decimal.TryParse(value, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                throw new ArgumentException($"Configuration field '{field.Key}' must be numeric.");
            }

            if (field.Type == "select" && field.Options is not null && !field.Options.Contains(value, StringComparer.Ordinal))
            {
                throw new ArgumentException($"Configuration field '{field.Key}' has an unsupported value.");
            }
        }

        foreach (var (key, value) in publicValues)
        {
            if (value is null)
            {
                throw new ArgumentException($"Configuration field '{key}' cannot be null.");
            }

            if (value.Length > 1000)
            {
                throw new ArgumentException($"Configuration field '{key}' is too long.");
            }
        }

        foreach (var (key, value) in secrets)
        {
            if (!secretKeys.Contains(key))
            {
                throw new ArgumentException($"Secret field '{key}' is invalid.");
            }

            if (value is null || value.Length > 4000)
            {
                throw new ArgumentException($"Secret field '{key}' cannot be null or too long.");
            }
        }
    }
}
