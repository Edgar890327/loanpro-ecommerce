using Microsoft.Extensions.Options;

namespace Store.Api.Auth;

public static class SessionCookie
{
    public static void Append(HttpContext context, SessionToken token, AuthOptions options)
    {
        context.Response.Cookies.Append(options.CookieName, token.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Path = "/",
            Expires = token.ExpiresAtUtc
        });
    }

    public static void Delete(HttpContext context, AuthOptions options)
    {
        context.Response.Cookies.Delete(options.CookieName, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Path = "/"
        });
    }
}
