using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Store.Api;
using Store.Api.Admin;
using Store.Api.Auth;
using Store.Api.E2e;
using Store.Application;
using Store.Domain;
using Store.Infrastructure;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Store.Api")
    .WriteTo.Console(
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);
var jwtOptions = new JwtOptions
{
    Secret = builder.Configuration["JWT_SECRET"] ?? string.Empty,
    ExpiryMinutes = builder.Configuration.GetValue("JWT_EXPIRY_MINUTES", 30)
};
var adminEmail = builder.Configuration["ADMIN_EMAIL"];
var adminPassword = builder.Configuration["ADMIN_PASSWORD"];
if (string.IsNullOrWhiteSpace(adminEmail) || !System.Net.Mail.MailAddress.TryCreate(adminEmail.Trim(), out _))
{
    throw new InvalidOperationException("ADMIN_EMAIL must be a valid email address.");
}

if (adminPassword is null || adminPassword.Length is < 8 or > 128)
{
    throw new InvalidOperationException("ADMIN_PASSWORD must contain between 8 and 128 characters.");
}

var authOptions = new AuthOptions
{
    CookieName = builder.Configuration["AUTH_COOKIE_NAME"] is { Length: > 0 } cookieName
        ? cookieName
        : "store_session",
    RateLimitPerMinute = builder.Configuration.GetValue("AUTH_RATE_LIMIT_PER_MINUTE", 10)
};
if (authOptions.RateLimitPerMinute < 1)
{
    throw new InvalidOperationException("AUTH_RATE_LIMIT_PER_MINUTE must be positive.");
}

if (Encoding.UTF8.GetByteCount(jwtOptions.Secret) < 32)
{
    throw new InvalidOperationException("JWT_SECRET must contain at least 32 bytes.");
}

if (jwtOptions.ExpiryMinutes is < 1 or > 60)
{
    throw new InvalidOperationException("JWT_EXPIRY_MINUTES must be between 1 and 60.");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<JwtOptions>(options =>
{
    options.Secret = jwtOptions.Secret;
    options.ExpiryMinutes = jwtOptions.ExpiryMinutes;
});
builder.Services.AddSingleton(Options.Create(authOptions));
builder.Services.AddSingleton<SessionTokenService>();
builder.Services.AddSingleton<EmailAttemptLimiter>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authOptions.RateLimitPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new Dictionary<string, object?>
            {
                ["status"] = StatusCodes.Status429TooManyRequests,
                ["title"] = "Too many attempts. Try again later.",
                ["code"] = "RATE_LIMITED"
            },
            cancellationToken);
    };
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies[authOptions.CookieName];
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(AuthorizationPolicies.Configure);

var app = builder.Build();

app.UseRouting();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    SuppressDiagnosticsCallback = _ => true
});
app.UseMiddleware<CsrfHeaderMiddleware>();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", async (IDatabaseHealthReader databaseHealth, CancellationToken cancellationToken) =>
{
    var databaseIsAvailable = await databaseHealth.IsAvailableAsync(cancellationToken);

    return databaseIsAvailable
        ? Results.Ok(new { status = "Healthy" })
        : Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Service unavailable",
            detail: "The database connection is unavailable.");
})
.WithName("Health");

app.MapAuthEndpoints();
app.MapCatalogEndpoints();
app.MapProductEndpoints();
app.MapProductImportEndpoints();
app.MapErrorLogEndpoints();
Store.Api.Admin.OrderHistoryEndpoints.MapOrderHistoryEndpoints(app);
Store.Api.Orders.OrderEndpoints.MapOrderEndpoints(app);
Store.Api.Me.MyOrdersEndpoints.MapMyOrdersEndpoints(app);
Store.Api.Payments.PaymentEndpoints.MapPaymentEndpoints(app);
Store.Api.Admin.PaymentProviderEndpoints.MapPaymentProviderEndpoints(app);
Store.Api.Admin.SettingsEndpoints.MapSettingsEndpoints(app);
Store.Api.Shipping.ShippingEndpoints.MapShippingEndpoints(app);

if (builder.Configuration.GetValue<bool>("E2E_MODE"))
{
    app.MapE2eEndpoints();
}

app.MapFallbackToFile("index.html");

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapPost(
        "/test/throw",
        (Func<IResult>)(() => throw new InvalidOperationException("private@example.com")));
}

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
    await database.Database.MigrateAsync();
    await scope.ServiceProvider
        .GetRequiredService<Store.Application.IPaymentProviderRegistry>()
        .EnsureInitializedAsync(CancellationToken.None);
    await scope.ServiceProvider
        .GetRequiredService<Store.Application.IShippingStrategyRegistry>()
        .EnsureInitializedAsync(CancellationToken.None);
    await scope.ServiceProvider
        .GetRequiredService<AdminSeeder>()
        .EnsureAsync(adminEmail, adminPassword, CancellationToken.None);
}

app.Run();

public partial class Program
{
}
