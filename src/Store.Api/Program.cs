using Microsoft.EntityFrameworkCore;
using Serilog;
using Store.Api;
using Store.Infrastructure;

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

var app = builder.Build();

app.UseRouting();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    SuppressDiagnosticsCallback = _ => true
});
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", async (StoreDbContext database, CancellationToken cancellationToken) =>
{
    var databaseIsAvailable = await database.Database.CanConnectAsync(cancellationToken);

    return databaseIsAvailable
        ? Results.Ok(new { status = "Healthy" })
        : Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Service unavailable",
            detail: "The database connection is unavailable.");
})
.WithName("Health");

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
}

app.Run();

public partial class Program
{
}
