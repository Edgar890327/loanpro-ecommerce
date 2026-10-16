using Serilog.Context;

namespace Store.Api;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var suppliedId = context.Request.Headers[HeaderName].ToString();
        var correlationId = Guid.TryParse(suppliedId, out var parsedId)
            ? parsedId.ToString("D")
            : Guid.NewGuid().ToString("D");

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
