using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Store.Api;
using Store.Application;
using Store.Domain;

namespace Store.UnitTests;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ReturnsProblemDetailsAndQueuesSafeErrorMetadata()
    {
        const string correlationId = "5c4e3ef0-674e-432c-b355-83cf0a6c109e";
        var context = CreateContext(correlationId);
        var writer = new FakeErrorLogWriter();
        var handler = CreateHandler(writer);

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("customer@example.com secret-value"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        context.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(
            correlationId,
            problem.RootElement.GetProperty("correlationId").GetString());

        var errorLog = Assert.IsType<ErrorLog>(writer.ErrorLog);
        Assert.Equal(correlationId, errorLog.CorrelationId);
        Assert.Equal("An unhandled exception occurred.", errorLog.Message);
        Assert.DoesNotContain("customer@example.com", errorLog.Message);
        Assert.DoesNotContain("secret-value", errorLog.Message);
        Assert.Equal("GET", errorLog.HttpMethod);
        Assert.Equal("/catalog/{productId}", errorLog.Path);
        Assert.Equal(StatusCodes.Status500InternalServerError, errorLog.StatusCode);
        Assert.Equal(DateTimeKind.Utc, errorLog.TimestampUtc.Kind);
        Assert.Null(errorLog.OrderId);
        Assert.Null(errorLog.PaymentProviderKey);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsProblemDetailsWhenWriterThrows()
    {
        var context = CreateContext("correlation-id");
        var writer = new FakeErrorLogWriter
        {
            Write = _ => throw new InvalidOperationException("queue unavailable")
        };
        var handler = CreateHandler(writer);

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("failure"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    private static DefaultHttpContext CreateContext(string correlationId)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/catalog/1";
        context.Response.Body = new MemoryStream();
        context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        context.Items[CorrelationIdMiddleware.RouteTemplateItemKey] = "/catalog/{productId}";
        return context;
    }

    private static GlobalExceptionHandler CreateHandler(IErrorLogWriter writer) =>
        new(writer, NullLogger<GlobalExceptionHandler>.Instance);

    private sealed class FakeErrorLogWriter : IErrorLogWriter
    {
        public ErrorLog? ErrorLog { get; private set; }

        public Func<ErrorLog, bool>? Write { get; init; }

        public bool TryWrite(ErrorLog errorLog)
        {
            ErrorLog = errorLog;
            return Write?.Invoke(errorLog) ?? true;
        }
    }
}
