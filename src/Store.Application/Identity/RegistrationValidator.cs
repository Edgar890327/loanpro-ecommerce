using System.Net.Mail;

namespace Store.Application;

public static class RegistrationValidator
{
    public const int MinPasswordLength = 8;

    public const int MaxPasswordLength = 128;

    public static IReadOnlyDictionary<string, string[]> Validate(RegisterInput input)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var email = input.Email?.Trim() ?? string.Empty;
        if (email.Length is 0 or > 320 ||
            !MailAddress.TryCreate(email, out var address) ||
            !string.Equals(address.Address, email, StringComparison.Ordinal))
        {
            errors["email"] = ["A valid email address of at most 320 characters is required."];
        }

        var fullName = input.FullName?.Trim() ?? string.Empty;
        if (fullName.Length is 0 or > 200)
        {
            errors["fullName"] = ["Full name is required and must be at most 200 characters."];
        }

        var passwordLength = input.Password?.Length ?? 0;
        if (passwordLength is < MinPasswordLength or > MaxPasswordLength)
        {
            errors["password"] = ["Password must contain between 8 and 128 characters."];
        }

        return errors;
    }
}
