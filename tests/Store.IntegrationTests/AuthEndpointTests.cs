using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Store.Infrastructure;

namespace Store.IntegrationTests;

public sealed class AuthEndpointTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private const string Password = "a-long-enough-password";

    [Fact]
    public async Task Register_CreatesACustomerAndSignsThemIn()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();
        var email = NewEmail();

        using var response = await Register(client, email);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, body);
        using var profile = JsonDocument.Parse(body);
        Assert.Equal("Customer", profile.RootElement.GetProperty("role").GetString());
        Assert.Equal("Test Customer", profile.RootElement.GetProperty("fullName").GetString());

        using var me = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Register_RejectsInvalidInput()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/auth/register",
            new { email = "nope", password = "short", fullName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_AllowsExactlyOneOfTwoSimultaneousRegistrationsWithTheSameEmail()
    {
        using var factory = new TestApiFactory(fixture);
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        var email = NewEmail();

        var responses = await Task.WhenAll(Register(first, email), Register(second, email.ToUpperInvariant()));
        try
        {
            var statuses = responses.Select(response => response.StatusCode).Order().ToArray();
            Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statuses);
            var conflict = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
            using var problem = JsonDocument.Parse(await conflict.Content.ReadAsStringAsync());
            Assert.Equal("EMAIL_TAKEN", problem.RootElement.GetProperty("code").GetString());
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Login_SetsAnHttpOnlyLaxCookie()
    {
        using var factory = new TestApiFactory(fixture);
        using var registrar = factory.CreateClient();
        var email = NewEmail();
        (await Register(registrar, email)).Dispose();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/auth/login", new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("store_session=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_RespondsIdenticallyForAnUnknownEmailAndAWrongPassword()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();
        var email = NewEmail();
        (await Register(client, email)).Dispose();
        using var anonymous = factory.CreateClient();

        using var wrong = await anonymous.PostAsJsonAsync("/auth/login", new { email, password = "wrong-password" });
        using var unknown = await anonymous.PostAsJsonAsync(
            "/auth/login",
            new { email = NewEmail(), password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(await Normalize(wrong), await Normalize(unknown));
        Assert.DoesNotContain(wrong.Headers, header => header.Key == "Set-Cookie");
    }

    [Fact]
    public async Task Me_RequiresASession()
    {
        using var factory = new TestApiFactory(fixture);
        using var anonymous = factory.CreateClient();

        using var response = await anonymous.GetAsync("/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsTheSession()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();
        (await Register(client, NewEmail())).Dispose();

        using var logout = await client.PostAsync("/auth/logout", null);
        using var me = await client.GetAsync("/me");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoints_AllowAdminsRejectAnonymousAndForbidCustomers()
    {
        using var factory = new TestApiFactory(fixture);
        using var anonymous = factory.CreateClient();
        using var customer = factory.CreateClient();
        using var admin = factory.CreateClient();
        (await Register(customer, NewEmail())).Dispose();
        await AuthTestClient.SignInAsync(admin, TestApiFactory.AdminEmail, factory.AdminPassword);

        foreach (var path in new[] { "/api/admin/products", "/api/admin/error-logs", "/api/admin/settings" })
        {
            using var adminResponse = await admin.GetAsync(path);
            using var anonymousResponse = await anonymous.GetAsync(path);
            using var customerResponse = await customer.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);
        }
    }

    [Fact]
    public async Task MutatingRequestsWithoutTheCustomHeaderAreForbidden()
    {
        using var factory = new TestApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove("X-Requested-With");

        using var response = await client.PostAsJsonAsync(
            "/auth/login",
            new { email = TestApiFactory.AdminEmail, password = factory.AdminPassword });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("FORBIDDEN", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AuthEndpoints_ReturnTooManyRequestsWhenTheLimitIsExceeded()
    {
        using var factory = new TestApiFactory(fixture, authRateLimitPerMinute: 3);
        using var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var response = await client.PostAsJsonAsync(
                "/auth/login",
                new { email = NewEmail(), password = "wrong-password" });
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.Equal(3, statuses.Count(status => status == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task LoginsAreLimitedPerEmail()
    {
        using var factory = new TestApiFactory(fixture, authRateLimitPerMinute: 2);
        using var client = factory.CreateClient();
        var email = NewEmail();

        using var first = await client.PostAsJsonAsync("/auth/login", new { email, password = "wrong-password" });
        using var second = await client.PostAsJsonAsync("/auth/login", new { email, password = "wrong-password" });
        using var third = await client.PostAsJsonAsync("/auth/login", new { email, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
    }

    [Fact]
    public async Task FailedLoginsNeverLeakTheEmailOrPasswordIntoLogsOrErrorLog()
    {
        var originalOut = Console.Out;
        var captured = new StringWriter();
        Console.SetOut(TextWriter.Synchronized(captured));
        try
        {
            using var factory = new TestApiFactory(fixture);
            using var client = factory.CreateClient();
            var email = NewEmail();
            const string secret = "super-secret-wrong-password";

            using var response = await client.PostAsJsonAsync("/auth/login", new { email, password = secret });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            using var throwing = await factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"))
                .CreateClient()
                .PostAsJsonAsync("/test/throw", new { email, password = secret });
            await Task.Delay(1500);

            var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<StoreDbContext>>();
            await using var database = await contextFactory.CreateDbContextAsync();
            var logs = await database.ErrorLogs.AsNoTracking().ToListAsync();
            var persisted = string.Join('\n', logs.Select(log =>
                $"{log.Message}{log.StackTrace}{log.Path}{log.ExceptionType}"));

            Assert.DoesNotContain(email, persisted, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(secret, persisted);
            var output = captured.ToString();
            Assert.DoesNotContain(email, output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(secret, output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static Task<HttpResponseMessage> Register(HttpClient client, string email) =>
        client.PostAsJsonAsync(
            "/auth/register",
            new { email, password = Password, fullName = "Test Customer", preferredLocale = "es" });

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static async Task<string> Normalize(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        return $"{root.GetProperty("status")}|{root.GetProperty("title")}|{root.GetProperty("code")}";
    }
}
