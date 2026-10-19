using Microsoft.AspNetCore.Authorization;
using Store.Domain;

namespace Store.Api.Auth;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";

    public const string CustomerOnly = "CustomerOnly";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(AdminOnly, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(UserRole.Admin.ToString()));
        options.AddPolicy(CustomerOnly, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(UserRole.Customer.ToString()));
    }
}
