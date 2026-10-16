using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Store.Application;
using Store.Domain;
using System.Text.Json;

namespace Store.Api;

public sealed class GlobalExceptionHandler(
    IErrorLogWriter errorLogWriter,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items.TryGetValue(
            CorrelationIdMiddleware.ContextItemKey,
            out var correlationIdValue)
            ? correlationIdValue as string
            : null;

        correlationId ??= httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = httpContext.TraceIdentifier;
        }
        httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;

        var path = httpContext.Items.TryGetValue(
            CorrelationIdMiddleware.RouteTemplateItemKey,
            out var routeTemplate)
            ? routeTemplate as string ?? string.Empty
            : string.Empty;

        var errorLog = new ErrorLog
        {
            TimestampUtc = DateTime.UtcNow,
            Level = "Error",
            CorrelationId = correlationId,
            Message = "An unhandled exception occurred.",
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            StackTrace = exception.StackTrace,
            HttpMethod = httpContext.Request.Method,
            Path = path,
            StatusCode = StatusCodes.Status500InternalServerError
        };

        try
        {
            if (!errorLogWriter.TryWrite(errorLog))
            {
                logger.LogError(
                    "Could not enqueue an error log for correlation ID {CorrelationId}; the error log queue is full.",
                    correlationId);
            }
        }
        catch (Exception writerException)
        {
            logger.LogError(
                writerException,
                "Could not enqueue an error log for correlation ID {CorrelationId}.",
                correlationId);
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred."
        };
        problem.Extensions["code"] = "unexpected_error";
        problem.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = problem.Status.Value;
        httpContext.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            problem,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken);

        return true;
    }
}
