using System.Text.Json;
using Store.Api.Auth;

namespace Store.Api;

public sealed class CsrfHeaderMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> ProtectedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (ProtectedMethods.Contains(context.Request.Method) &&
            !string.Equals(
                context.Request.Headers[AuthOptions.CsrfHeaderName].ToString(),
                AuthOptions.CsrfHeaderValue,
                StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                new Dictionary<string, object?>
                {
                    ["status"] = StatusCodes.Status403Forbidden,
                    ["title"] = "A required request header is missing.",
                    ["code"] = "FORBIDDEN"
                });
            return;
        }

        await next(context);
    }
}
