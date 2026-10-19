using Microsoft.AspNetCore.Http;
using Store.Api;
using Store.Api.Auth;

namespace Store.UnitTests;

public sealed class CsrfHeaderMiddlewareTests
{
    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Invoke_RejectsMutatingRequestsWithoutTheHeader(string method)
    {
        var (called, context) = await Run(method, null);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        Assert.Contains("FORBIDDEN", await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    [Fact]
    public async Task Invoke_RejectsAWrongHeaderValue()
    {
        var (called, context) = await Run("POST", "other");

        Assert.False(called);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("DELETE")]
    public async Task Invoke_AllowsMutatingRequestsWithTheHeader(string method)
    {
        var (called, context) = await Run(method, AuthOptions.CsrfHeaderValue);

        Assert.True(called);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task Invoke_AllowsSafeMethodsWithoutTheHeader(string method)
    {
        var (called, _) = await Run(method, null);

        Assert.True(called);
    }

    private static async Task<(bool Called, DefaultHttpContext Context)> Run(string method, string? headerValue)
    {
        var called = false;
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Method = method;
        if (headerValue is not null)
        {
            context.Request.Headers[AuthOptions.CsrfHeaderName] = headerValue;
        }

        await new CsrfHeaderMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }).InvokeAsync(context);

        return (called, context);
    }
}
