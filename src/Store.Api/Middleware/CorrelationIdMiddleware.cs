using Serilog.Context;
using Microsoft.AspNetCore.Routing;

namespace Store.Api;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ContextItemKey = "CorrelationId";
    public const string RouteTemplateItemKey = "RouteTemplate";

    public async Task InvokeAsync(HttpContext context)
    {
        var suppliedId = context.Request.Headers[HeaderName].ToString();
        var correlationId = Guid.TryParse(suppliedId, out var parsedId)
            ? parsedId.ToString("D")
            : Guid.NewGuid().ToString("D");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        context.Items[ContextItemKey] = correlationId;
        context.Items[RouteTemplateItemKey] =
            (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? string.Empty;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
