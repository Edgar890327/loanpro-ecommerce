using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Store.Application;
using Store.Domain;
using Store.Infrastructure.Payments;

namespace Store.Infrastructure.Orders;

public sealed class ShippingStrategyRegistry : IShippingStrategyRegistry
{
    private readonly IDbContextFactory<StoreDbContext> _contextFactory;
    private readonly IReadOnlyDictionary<string, IShippingStrategy> _strategies;
    private readonly PaymentSecretsProtector _secretsProtector;
    private readonly TimeProvider _timeProvider;

    public ShippingStrategyRegistry(
        IDbContextFactory<StoreDbContext> contextFactory,
        IEnumerable<IShippingStrategy> strategies,
        PaymentSecretsProtector secretsProtector,
        TimeProvider timeProvider)
    {
        _contextFactory = contextFactory;
        _secretsProtector = secretsProtector;
        _timeProvider = timeProvider;
        var strategyArray = strategies.ToArray();
        if (strategyArray.Select(strategy => strategy.Descriptor.Key)
            .Distinct(StringComparer.Ordinal).Count() != strategyArray.Length)
        {
            throw new InvalidOperationException("Shipping strategy keys must be unique.");
        }

        _strategies = strategyArray.ToDictionary(
            strategy => strategy.Descriptor.Key,
            StringComparer.Ordinal);
        Descriptors = strategyArray.Select(strategy => strategy.Descriptor)
            .OrderBy(descriptor => descriptor.Name)
            .ToArray();
    }

    public IReadOnlyList<ShippingStrategyDescriptor> Descriptors { get; }

    public async Task<IReadOnlyList<ShippingStrategyView>> ListAsync(CancellationToken cancellationToken)
    {
        await using var database = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var settings = await database.ShippingStrategySettings
            .AsNoTracking()
            .ToDictionaryAsync(setting => setting.StrategyKey, cancellationToken);
        return Descriptors.Select(descriptor =>
        {
            if (!settings.TryGetValue(descriptor.Key, out var setting))
            {
                return new ShippingStrategyView(
                    descriptor,
                    _strategies[descriptor.Key].DefaultConfiguration,
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    false,
                    false);
            }

            return ToView(setting);
        }).ToArray();
    }

    public async Task<ShippingStrategyView> SaveAsync(
        string strategyKey,
        ShippingStrategySettingInput input,
        CancellationToken cancellationToken)
    {
        var strategy = Resolve(strategyKey);
        if (input.Activate && !input.Enabled)
        {
            throw new InvalidOperationException("An inactive shipping strategy cannot be selected.");
        }

        if (input.Secrets.Keys.Any(key => !strategy.Descriptor.SecretFields.Contains(key, StringComparer.Ordinal)))
        {
            throw new ArgumentException("Shipping configuration contains unsupported secret fields.");
        }

        await using var database = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AcquireSettingsLockAsync(database, cancellationToken);
        var settings = await database.ShippingStrategySettings.ToListAsync(cancellationToken);
        var existing = settings.SingleOrDefault(setting => setting.StrategyKey == strategyKey);
        var currentSecrets = existing is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(
                _secretsProtector.Unprotect(existing.ProtectedSecrets),
                StringComparer.Ordinal);
        foreach (var key in strategy.Descriptor.SecretFields)
        {
            if (input.Secrets.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                currentSecrets[key] = value;
            }
        }
        ConfigurationSchemaValidator.Validate(
            strategy.Descriptor.ConfigurationFields,
            strategy.Descriptor.SecretFields,
            input.PublicConfiguration,
            currentSecrets,
            input.Activate || existing?.IsActive == true);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var protectedSecrets = _secretsProtector.Protect(currentSecrets);
        var publicConfiguration = JsonSerializer.Serialize(input.PublicConfiguration);
        if (existing is null)
        {
            existing = new ShippingStrategySetting(
                strategyKey,
                publicConfiguration,
                protectedSecrets,
                input.Enabled,
                input.Activate,
                now);
            database.ShippingStrategySettings.Add(existing);
        }
        else
        {
            existing.Update(publicConfiguration, protectedSecrets, input.Enabled, now);
        }

        if (!input.Enabled && existing.IsActive)
        {
            throw new InvalidOperationException("The active shipping strategy cannot be disabled.");
        }

        if (input.Activate)
        {
            if (database.ShippingStrategySettings.Local.Any(setting =>
                    setting.IsActive && !ReferenceEquals(setting, existing)))
            {
                await database.Database.ExecuteSqlRawAsync(
                    "UPDATE \"ShippingStrategySettings\" SET \"IsActive\" = FALSE WHERE \"IsActive\" = TRUE",
                    cancellationToken);
            }

            foreach (var setting in settings.Where(setting => setting.IsActive))
            {
                setting.SetActive(false, now);
            }

            if (database.Entry(existing).State != EntityState.Added)
            {
                database.Entry(existing).Property(setting => setting.IsActive).OriginalValue = false;
            }

            existing.SetActive(true, now);
        }

        await database.SaveChangesAsync(cancellationToken);
        ActiveSettingInvariant.EnsureExactlyOne(await database.ShippingStrategySettings.CountAsync(
            setting => setting.IsActive && setting.IsEnabled,
            cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return ToView(existing);
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        await using var database = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        await AcquireSettingsLockAsync(database, cancellationToken);
        var settings = await database.ShippingStrategySettings.ToListAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var hasSettings = settings.Count > 0;
        var hasActiveSetting = settings.Any(setting => setting.IsActive);
        foreach (var strategy in _strategies.Values)
        {
            if (settings.All(setting => setting.StrategyKey != strategy.Descriptor.Key))
            {
                var isActive = !hasSettings && !hasActiveSetting;
                database.ShippingStrategySettings.Add(new ShippingStrategySetting(
                    strategy.Descriptor.Key,
                    JsonSerializer.Serialize(strategy.DefaultConfiguration),
                    _secretsProtector.Protect(new Dictionary<string, string>(StringComparer.Ordinal)),
                    true,
                    isActive,
                    now));
                hasSettings = true;
                hasActiveSetting |= isActive;
            }
        }

        await database.SaveChangesAsync(cancellationToken);

        ActiveSettingInvariant.EnsureExactlyOne(await database.ShippingStrategySettings.CountAsync(
            setting => setting.IsActive && setting.IsEnabled,
            cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ShippingCalculation> CalculateAsync(
        ShippingContext context,
        CancellationToken cancellationToken)
    {
        await using var database = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var setting = await database.ShippingStrategySettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.IsActive && item.IsEnabled, cancellationToken)
            ?? throw new InvalidOperationException("No active shipping strategy is configured.");
        var configuration = new ShippingStrategyConfiguration(
            JsonSerializer.Deserialize<Dictionary<string, string>>(setting.PublicConfigurationJson)
                ?? new Dictionary<string, string>(StringComparer.Ordinal),
            _secretsProtector.Unprotect(setting.ProtectedSecrets));
        return new ShippingCalculation(
            setting.StrategyKey,
            Resolve(setting.StrategyKey).CalculateShipping(context, configuration));
    }

    private IShippingStrategy Resolve(string key) =>
        _strategies.TryGetValue(key, out var strategy)
            ? strategy
            : throw new KeyNotFoundException($"Shipping strategy '{key}' is not registered.");

    private ShippingStrategyView ToView(ShippingStrategySetting setting)
    {
        var secrets = _secretsProtector.Unprotect(setting.ProtectedSecrets);
        return new ShippingStrategyView(
            Resolve(setting.StrategyKey).Descriptor,
            JsonSerializer.Deserialize<Dictionary<string, string>>(setting.PublicConfigurationJson)
                ?? new Dictionary<string, string>(StringComparer.Ordinal),
            SecretMasker.Mask(secrets.Keys),
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
