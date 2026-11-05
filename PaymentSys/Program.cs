using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentSys.Authentication;
using PaymentSys.BackgroundServices;
using PaymentSys.Data;
using PaymentSys.Middleware;
using PaymentSys.Options;
using PaymentSys.Services.Idempotency;
using PaymentSys.Services.Ledger;
using PaymentSys.Services.Payments;
using PaymentSys.Services.Paystack;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ILedgerPostingService, LedgerPostingService>();

builder.Services.Configure<PaystackOptions>(
    builder.Configuration.GetSection(PaystackOptions.SectionName));
builder.Services.Configure<LedgerOptions>(
    builder.Configuration.GetSection(LedgerOptions.SectionName));
builder.Services.Configure<IdempotencyOptions>(
    builder.Configuration.GetSection(IdempotencyOptions.SectionName));
builder.Services.Configure<MerchantOptions>(
    builder.Configuration.GetSection(MerchantOptions.SectionName));

builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization();

var rateLimitOptions = builder.Configuration
    .GetSection(RateLimitOptions.SectionName)
    .Get<RateLimitOptions>() ?? new RateLimitOptions();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("payments", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.PermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                QueueLimit = 0
            }));
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
        options.AddPolicy("Default", policy =>
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()));
}

builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();

builder.Services.AddHostedService<RetentionCleanupService>();

builder.Services.AddHttpClient<IPaystackClient, PaystackClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<PaystackOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
        ? "https://api.paystack.co"
        : options.BaseUrl;

    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 30);
});

builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPaystackSignatureVerifier, PaystackSignatureVerifier>();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("PaymentSys API"));

app.MapGet("/", () => Results.Redirect("/scalar/v1"))
    .ExcludeFromDescription();

app.MapGet("/health", async (
    PaymentDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

    var payload = new
    {
        status = canConnect ? "healthy" : "unhealthy",
        database = canConnect ? "reachable" : "unreachable",
        timestamp = DateTime.UtcNow
    };

    return canConnect
        ? Results.Ok(payload)
        : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
})
    .AllowAnonymous()
    .ExcludeFromDescription();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

    await dbContext.Database.MigrateAsync();
    await LedgerAccountSeeder.SeedAsync(dbContext);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

if (allowedOrigins.Length > 0)
{
    app.UseCors("Default");
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapControllers();

app.Run();

public partial class Program;
