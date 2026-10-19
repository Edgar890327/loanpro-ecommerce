using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Store.Api.Auth;

namespace Store.UnitTests;

public sealed class AuthorizationPoliciesTests
{
    private readonly IAuthorizationService _authorization;

    public AuthorizationPoliciesTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(AuthorizationPolicies.Configure);
        _authorization = services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    [Theory]
    [InlineData("Admin", AuthorizationPolicies.AdminOnly, true)]
    [InlineData("Customer", AuthorizationPolicies.AdminOnly, false)]
    [InlineData("Customer", AuthorizationPolicies.CustomerOnly, true)]
    [InlineData("Admin", AuthorizationPolicies.CustomerOnly, false)]
    public async Task Policies_AllowOnlyTheMatchingRole(string role, string policy, bool allowed)
    {
        var result = await _authorization.AuthorizeAsync(Principal(role), null, policy);

        Assert.Equal(allowed, result.Succeeded);
    }

    [Theory]
    [InlineData(AuthorizationPolicies.AdminOnly)]
    [InlineData(AuthorizationPolicies.CustomerOnly)]
    public async Task Policies_RejectAnonymousUsers(string policy)
    {
        var result = await _authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, policy);

        Assert.False(result.Succeeded);
    }

    private static ClaimsPrincipal Principal(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
}
