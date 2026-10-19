namespace Store.Domain;

public sealed class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Email { get; private set; } = string.Empty;

    public string EmailNormalized { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public string? PreferredLocale { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? LastLoginAtUtc { get; private set; }

    private User()
    {
    }

    public User(
        string email,
        string emailNormalized,
        string passwordHash,
        string fullName,
        UserRole role,
        DateTime createdAtUtc,
        string? preferredLocale = null)
    {
        Email = email;
        EmailNormalized = emailNormalized;
        PasswordHash = passwordHash;
        FullName = fullName;
        Role = role;
        CreatedAtUtc = createdAtUtc;
        PreferredLocale = preferredLocale;
    }

    public void RecordLogin(DateTime atUtc)
    {
        LastLoginAtUtc = atUtc;
    }
}
