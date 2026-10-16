using Microsoft.AspNetCore.Http;
using Store.Api;

namespace Store.UnitTests;

public sealed class CorrelationIdMiddlewareTests
{
    [Theory]
    [InlineData("e2c2bc85-c838-4b6e-8e39-1508b051924e", "e2c2bc85-c838-4b6e-8e39-1508b051924e")]
    [InlineData("not-a-guid", null)]
    public async Task InvokeAsync_ReturnsAValidCorrelationId(string requestId, string? expectedId)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = requestId;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var responseId = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        Assert.True(Guid.TryParse(responseId, out _));

        if (expectedId is not null)
        {
            Assert.Equal(expectedId, responseId);
        }
        else
        {
            Assert.NotEqual(requestId, responseId);
        }
    }
}
