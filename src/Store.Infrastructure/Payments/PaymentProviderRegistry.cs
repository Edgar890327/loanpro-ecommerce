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
            .SingleOrDefaultAsync(item => item.IsActive && item.IsEnabled, cancellationToken);
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
        await using var database = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AcquireSettingsLockAsync(database, cancellationToken);
        var settings = await database.PaymentProviderSettings.ToListAsync(cancellationToken);
        var existing = settings.SingleOrDefault(item => item.ProviderKey == providerKey);
        if (existing?.IsActive == true && !input.Enabled)
        {
            throw new InvalidOperationException("The active payment provider cannot be disabled.");
        }

        if (input.Activate && !input.Enabled)
        {
            throw new InvalidOperationException("A disabled payment provider cannot be activated.");
        }

        if (input.Secrets.Keys.Any(key => !provider.Descriptor.SecretFields.Contains(key, StringComparer.Ordinal)))
        {
            throw new ArgumentException("Payment provider configuration contains unsupported secret fields.");
        }

        var currentSecrets = existing is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(
                _secretsProtector.Unprotect(existing.ProtectedSecrets),
                StringComparer.Ordinal);
        foreach (var secretField in provider.Descriptor.SecretFields)
        {
            if (input.Secrets.TryGetValue(secretField, out var secret) && !string.IsNullOrWhiteSpace(secret))
            {
                currentSecrets[secretField] = secret;
            }
        }
        ConfigurationSchemaValidator.Validate(
            provider.Descriptor.ConfigurationFields,
            provider.Descriptor.SecretFields,
            input.PublicConfiguration,
            currentSecrets,
            input.Activate || existing?.IsActive == true);

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
                now,
                input.Enabled);
            database.PaymentProviderSettings.Add(existing);
        }
        else
        {
            existing.Update(publicConfigurationJson, protectedSecrets, now);
            existing.SetEnabled(input.Enabled, now);
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

        ActiveSettingInvariant.EnsureExactlyOne(await database.PaymentProviderSettings.CountAsync(
            item => item.IsActive && item.IsEnabled,
            cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }

    private PaymentProviderView ToView(PaymentProviderSetting setting)
    {
        var publicValues = JsonSerializer.Deserialize<Dictionary<string, string>>(setting.PublicConfigurationJson)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        return new PaymentProviderView(
            Resolve(setting.ProviderKey).Descriptor,
            publicValues,
            SecretMasker.Mask(_secretsProtector.Unprotect(setting.ProtectedSecrets).Keys),
            setting.IsEnabled,
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

}
