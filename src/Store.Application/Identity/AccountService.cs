using Store.Domain;

namespace Store.Application;

public sealed class AccountService(
    IUserRepository users,
    IPasswordHasher hasher,
    TimeProvider timeProvider) : IAccountService
{
    public async Task<AccountResult> RegisterAsync(RegisterInput input, CancellationToken cancellationToken)
    {
        var errors = RegistrationValidator.Validate(input);
        if (errors.Count > 0)
        {
            return new AccountResult(AccountOutcome.ValidationFailed, Errors: errors);
        }

        var email = input.Email!.Trim();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = new User(
            email,
            EmailNormalizer.Normalize(email),
            hasher.Hash(input.Password!),
            input.FullName!.Trim(),
            UserRole.Customer,
            now,
            NormalizeLocale(input.PreferredLocale));
        user.RecordLogin(now);

        return await users.TryAddAsync(user, cancellationToken)
            ? new AccountResult(AccountOutcome.Succeeded, ToProfile(user))
            : new AccountResult(AccountOutcome.EmailTaken);
    }

    public async Task<AccountResult> LoginAsync(LoginInput input, CancellationToken cancellationToken)
    {
        var password = input.Password ?? string.Empty;
        var user = string.IsNullOrWhiteSpace(input.Email)
            ? null
            : await users.FindByEmailAsync(EmailNormalizer.Normalize(input.Email), cancellationToken);

        if (user is null)
        {
            hasher.Verify(hasher.DummyHash, password);
            return new AccountResult(AccountOutcome.InvalidCredentials);
        }

        if (!hasher.Verify(user.PasswordHash, password) || !user.IsActive)
        {
            return new AccountResult(AccountOutcome.InvalidCredentials);
        }

        await users.RecordLoginAsync(user.Id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return new AccountResult(AccountOutcome.Succeeded, ToProfile(user));
    }

    public async Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId, cancellationToken);
        return user is { IsActive: true } ? ToProfile(user) : null;
    }

    private static UserProfile ToProfile(User user) =>
        new(user.Id, user.FullName, user.Role, user.PreferredLocale);

    private static string? NormalizeLocale(string? locale) =>
        locale is "es" or "en-US" ? locale : null;
}
