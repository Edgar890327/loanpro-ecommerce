using Store.Application;
using Store.Domain;

namespace Store.UnitTests.Identity;

public sealed class AccountServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();

    private AccountService CreateService() => new(_users, _hasher, new FixedTimeProvider(Now));

    [Fact]
    public async Task Register_CreatesACustomerWithANormalizedEmailAndHashedPassword()
    {
        var result = await CreateService().RegisterAsync(
            new RegisterInput("  Ana@Example.COM ", "password-1", " Ana Perez ", "es"),
            CancellationToken.None);

        Assert.Equal(AccountOutcome.Succeeded, result.Outcome);
        var user = Assert.Single(_users.Users);
        Assert.Equal("ana@example.com", user.EmailNormalized);
        Assert.Equal("Ana Perez", user.FullName);
        Assert.Equal(UserRole.Customer, user.Role);
        Assert.Equal("hash:password-1", user.PasswordHash);
        Assert.Equal("es", result.Profile!.PreferredLocale);
        Assert.Equal(Now.UtcDateTime, user.LastLoginAtUtc);
    }

    [Fact]
    public async Task Register_ReportsValidationErrors()
    {
        var result = await CreateService().RegisterAsync(
            new RegisterInput("bad", "short", "", null),
            CancellationToken.None);

        Assert.Equal(AccountOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("email", result.Errors!.Keys);
        Assert.Contains("password", result.Errors.Keys);
        Assert.Contains("fullName", result.Errors.Keys);
        Assert.Empty(_users.Users);
    }

    [Fact]
    public async Task Register_ReportsADuplicateEmailIgnoringCase()
    {
        var service = CreateService();
        await service.RegisterAsync(new RegisterInput("ana@example.com", "password-1", "Ana", null), CancellationToken.None);

        var result = await service.RegisterAsync(
            new RegisterInput("ANA@example.com", "password-2", "Other", null),
            CancellationToken.None);

        Assert.Equal(AccountOutcome.EmailTaken, result.Outcome);
        Assert.Single(_users.Users);
    }

    [Fact]
    public async Task Login_SucceedsWithValidCredentialsAndRecordsTheLogin()
    {
        var service = CreateService();
        await service.RegisterAsync(new RegisterInput("ana@example.com", "password-1", "Ana", null), CancellationToken.None);

        var result = await service.LoginAsync(new LoginInput("ANA@example.com", "password-1"), CancellationToken.None);

        Assert.Equal(AccountOutcome.Succeeded, result.Outcome);
        Assert.Equal("Ana", result.Profile!.FullName);
    }

    [Fact]
    public async Task Login_RespondsTheSameForAnUnknownEmailAndAWrongPassword()
    {
        var service = CreateService();
        await service.RegisterAsync(new RegisterInput("ana@example.com", "password-1", "Ana", null), CancellationToken.None);
        var verifyCallsBefore = _hasher.VerifyCalls;

        var unknown = await service.LoginAsync(new LoginInput("nobody@example.com", "password-1"), CancellationToken.None);
        var verifyCallsAfterUnknown = _hasher.VerifyCalls;
        var wrong = await service.LoginAsync(new LoginInput("ana@example.com", "wrong-password"), CancellationToken.None);

        Assert.Equal(wrong, unknown);
        Assert.Equal(AccountOutcome.InvalidCredentials, unknown.Outcome);
        Assert.Null(unknown.Profile);
        Assert.Equal(verifyCallsBefore + 1, verifyCallsAfterUnknown);
        Assert.Equal(verifyCallsAfterUnknown + 1, _hasher.VerifyCalls);
    }

    [Fact]
    public async Task Login_RejectsABlankEmail()
    {
        var result = await CreateService().LoginAsync(new LoginInput("  ", "password-1"), CancellationToken.None);

        Assert.Equal(AccountOutcome.InvalidCredentials, result.Outcome);
    }

    [Fact]
    public async Task GetProfile_ReturnsNullForUnknownUsers()
    {
        Assert.Null(await CreateService().GetProfileAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetProfile_ReturnsTheProfileWithoutSensitiveFields()
    {
        var service = CreateService();
        var registered = await service.RegisterAsync(
            new RegisterInput("ana@example.com", "password-1", "Ana", "xx"),
            CancellationToken.None);

        var profile = await service.GetProfileAsync(registered.Profile!.Id, CancellationToken.None);

        Assert.Equal(new UserProfile(registered.Profile.Id, "Ana", UserRole.Customer, null, "ana@example.com"), profile);
    }
}
