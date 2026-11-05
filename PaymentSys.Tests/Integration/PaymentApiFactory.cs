using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PaymentSys.Data;
using PaymentSys.Services.Paystack;
using PaymentSys.Tests.TestHelpers;

namespace PaymentSys.Tests.Integration
{
    public sealed class PaymentApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public const string ApiKey = "integration-test-api-key";
        public const string SecretKey = "integration-test-paystack-secret";
        public const string ClientId = "integration-test-client";

        private readonly string _databaseName = $"paymentsys-integration-{Guid.NewGuid():N}";

        public FakePaystackClient Paystack { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Merchant:ApiKey"] = ApiKey,
                    ["Merchant:ClientId"] = ClientId,
                    ["Paystack:SecretKey"] = SecretKey,
                    ["Paystack:BaseUrl"] = "https://api.paystack.co",
                    ["Paystack:TimeoutSeconds"] = "30",
                    ["RateLimit:PermitLimit"] = "1000",
                    ["RateLimit:WindowSeconds"] = "60"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<PaymentDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<PaymentDbContext>>();
                services.RemoveAll<PaymentDbContext>();

                services.AddDbContext<PaymentDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

                services.RemoveAll<IPaystackClient>();
                services.AddSingleton(Paystack);
                services.AddSingleton<IPaystackClient>(
                    provider => provider.GetRequiredService<FakePaystackClient>());
            });
        }

        public async Task InitializeAsync()
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

            await dbContext.Database.EnsureCreatedAsync();
            await LedgerAccountSeeder.SeedAsync(dbContext);
        }

        Task IAsyncLifetime.DisposeAsync() => base.DisposeAsync().AsTask();
    }
}
