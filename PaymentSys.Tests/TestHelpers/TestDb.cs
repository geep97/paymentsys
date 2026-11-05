using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PaymentSys.Data;
using PaymentSys.Models.Ledger;

namespace PaymentSys.Tests.TestHelpers
{
    internal static class TestDb
    {
        public static PaymentDbContext Create()
        {
            var options = new DbContextOptionsBuilder<PaymentDbContext>()
                .UseInMemoryDatabase($"paymentsys-tests-{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new PaymentDbContext(options);
        }

        public static async Task<(Account Settlement, Account Revenue)> SeedAccountsAsync(
            PaymentDbContext dbContext)
        {
            await LedgerAccountSeeder.SeedAsync(dbContext);

            var settlement = await dbContext.Accounts
                .SingleAsync(a => a.AccountNumber == "1000");
            var revenue = await dbContext.Accounts
                .SingleAsync(a => a.AccountNumber == "4000");

            return (settlement, revenue);
        }
    }
}
