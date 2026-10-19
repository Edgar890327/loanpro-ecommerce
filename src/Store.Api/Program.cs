using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Store.Api;
using Store.Api.Admin;
using Store.Api.Auth;
using Store.Api.E2e;
using Store.Application;
using Store.Infrastructure;
using System.Text;

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
    ExpiryMinutes = builder.Configuration.GetValue("JWT_EXPIRY_MINUTES", 15)
};
if (string.IsNullOrWhiteSpace(builder.Configuration["ADMIN_PASSWORD"]))
{
    throw new InvalidOperationException("ADMIN_PASSWORD is required.");
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
builder.Services.AddSingleton<AdminTokenService>();
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
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy
        .RequireAuthenticatedUser()
        .RequireRole("Administrator"));
});

var app = builder.Build();

app.UseRouting();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    SuppressDiagnosticsCallback = _ => true
});
app.UseAuthentication();
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
}

app.Run();

public partial class Program
{
}
