using Store.Application;

namespace Store.Api.Auth;

public sealed record UserProfileResponse(Guid Id, string FullName, string Role, string? PreferredLocale, string Email)
{
    public static UserProfileResponse From(UserProfile profile) =>
        new(profile.Id, profile.FullName, profile.Role.ToString(), profile.PreferredLocale, profile.Email);
}
