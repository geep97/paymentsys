using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentSys.Data;
using PaymentSys.Middleware;
using PaymentSys.Options;
using PaymentSys.Services.Idempotency;
using PaymentSys.Services.Ledger;
using PaymentSys.Services.Payments;
using PaymentSys.Services.Paystack;

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

builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

    await dbContext.Database.MigrateAsync();
    await LedgerAccountSeeder.SeedAsync(dbContext);
}

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();
