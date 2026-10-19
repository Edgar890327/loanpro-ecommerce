using Store.Domain;

namespace Store.Application;

public sealed record UserProfile(Guid Id, string FullName, UserRole Role, string? PreferredLocale, string Email);
