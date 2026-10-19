using System.Net;
using System.Net.Http.Json;

namespace Store.IntegrationTests;

public static class AuthTestClient
{
    public static async Task SignInAsync(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsJsonAsync("/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public static async Task<string> RegisterCustomerAsync(HttpClient client)
    {
        var email = $"customer-{Guid.NewGuid():N}@example.com";
        using var response = await client.PostAsJsonAsync(
            "/auth/register",
            new { email, password = "customer-password-1", fullName = "Test Customer" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return email;
    }
}
