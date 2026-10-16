using Store.Api.Auth;

namespace Store.Api;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");
        group.MapPost("/login", LoginAsync).AllowAnonymous();
        return group;
    }

    private static IResult LoginAsync(
        LoginRequest request,
        IConfiguration configuration,
        AdminTokenService tokenService)
    {
        var configuredPassword = configuration["ADMIN_PASSWORD"];
        if (string.IsNullOrEmpty(configuredPassword) ||
            !AdminTokenService.PasswordMatches(request.Password ?? string.Empty, configuredPassword))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid credentials");
        }

        return Results.Ok(tokenService.Issue());
    }

    public sealed record LoginRequest(string? Password);
}
