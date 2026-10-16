using System.Globalization;

namespace Store.Application;

public static class ConfigurationSchemaValidator
{
    public static void Validate(
        IReadOnlyList<ConfigurationFieldDescriptor> fields,
        IReadOnlyList<string> secretFields,
        IReadOnlyDictionary<string, string> publicValues,
        IReadOnlyDictionary<string, string> secrets,
        bool requireRequiredFields)
    {
        var publicFields = fields.Where(field => !field.Secret)
            .ToDictionary(field => field.Key, StringComparer.Ordinal);
        var secretKeys = secretFields.ToHashSet(StringComparer.Ordinal);
        if (publicValues.Keys.Any(key => !publicFields.ContainsKey(key)) ||
            secrets.Keys.Any(key => !secretKeys.Contains(key)))
        {
            throw new ArgumentException("Configuration contains unsupported fields.");
        }

        foreach (var field in fields.Where(field => !field.Secret))
        {
            if (!publicValues.TryGetValue(field.Key, out var value))
            {
                if (requireRequiredFields && field.Required)
                {
                    throw new ArgumentException($"Configuration field '{field.Key}' is required.");
                }

                continue;
            }

            if (value is null || value.Length > 1000 ||
                field.Required && requireRequiredFields && string.IsNullOrWhiteSpace(value) ||
                field.Type == "boolean" && !bool.TryParse(value, out _) ||
                field.Type == "number" &&
                (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ||
                 field.Minimum.HasValue && number < field.Minimum.Value ||
                 field.Maximum.HasValue && number > field.Maximum.Value) ||
                field.Type == "select" && field.Options is not null &&
                !field.Options.Contains(value, StringComparer.Ordinal))
            {
                throw new ArgumentException($"Configuration field '{field.Key}' is invalid.");
            }
        }

        foreach (var field in fields.Where(field => field.Secret && field.Required))
        {
            if (requireRequiredFields &&
                (!secrets.TryGetValue(field.Key, out var value) || string.IsNullOrWhiteSpace(value)))
            {
                throw new ArgumentException($"Secret field '{field.Key}' is required.");
            }
        }

        foreach (var (key, value) in secrets)
        {
            if (value is null || value.Length > 4000)
            {
                throw new ArgumentException($"Secret field '{key}' is invalid.");
            }
        }
    }
}
