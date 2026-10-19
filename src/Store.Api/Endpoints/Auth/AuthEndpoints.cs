using Microsoft.Extensions.Options;
using Store.Api.Auth;
using Store.Application;

namespace Store.Api;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth").RequireRateLimiting("auth");
        group.MapPost("/register", RegisterAsync).AllowAnonymous();
        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapPost("/logout", Logout).AllowAnonymous();
        endpoints.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        HttpContext context,
        IAccountService accounts,
        SessionTokenService tokens,
        EmailAttemptLimiter limiter,
        IOptions<AuthOptions> options,
        CancellationToken cancellationToken)
    {
        if (!limiter.TryAcquire(request.Email))
        {
            return Problem(StatusCodes.Status429TooManyRequests, "RATE_LIMITED", "Too many attempts. Try again later.");
        }

        var result = await accounts.RegisterAsync(
            new RegisterInput(request.Email, request.Password, request.FullName, request.PreferredLocale),
            cancellationToken);

        switch (result.Outcome)
        {
            case AccountOutcome.ValidationFailed:
                return Results.ValidationProblem(result.Errors!);
            case AccountOutcome.EmailTaken:
                return Problem(StatusCodes.Status409Conflict, "EMAIL_TAKEN", "The email address is already registered.");
            default:
                SessionCookie.Append(context, tokens.Issue(result.Profile!), options.Value);
                return Results.Created("/me", UserProfileResponse.From(result.Profile!));
        }
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext context,
        IAccountService accounts,
        SessionTokenService tokens,
        EmailAttemptLimiter limiter,
        IOptions<AuthOptions> options,
        CancellationToken cancellationToken)
    {
        if (!limiter.TryAcquire(request.Email))
        {
            return Problem(StatusCodes.Status429TooManyRequests, "RATE_LIMITED", "Too many attempts. Try again later.");
        }

        var result = await accounts.LoginAsync(new LoginInput(request.Email, request.Password), cancellationToken);
        if (result.Outcome != AccountOutcome.Succeeded)
        {
            return Problem(StatusCodes.Status401Unauthorized, "INVALID_CREDENTIALS", "Invalid credentials.");
        }

        SessionCookie.Append(context, tokens.Issue(result.Profile!), options.Value);
        return Results.Ok(UserProfileResponse.From(result.Profile!));
    }

    private static IResult Logout(HttpContext context, IOptions<AuthOptions> options)
    {
        SessionCookie.Delete(context, options.Value);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext context,
        IAccountService accounts,
        IOptions<AuthOptions> options,
        CancellationToken cancellationToken)
    {
        var userId = context.User.GetUserId();
        var profile = userId.HasValue
            ? await accounts.GetProfileAsync(userId.Value, cancellationToken)
            : null;
        if (profile is null)
        {
            SessionCookie.Delete(context, options.Value);
            return Problem(StatusCodes.Status401Unauthorized, "SESSION_EXPIRED", "The session is no longer valid.");
        }

        return Results.Ok(UserProfileResponse.From(profile));
    }

    private static IResult Problem(int status, string code, string title) =>
        Results.Problem(
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
