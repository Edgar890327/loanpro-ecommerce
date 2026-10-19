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
}
