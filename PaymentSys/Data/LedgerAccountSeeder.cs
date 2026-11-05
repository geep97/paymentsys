using Microsoft.EntityFrameworkCore;
using PaymentSys.Models.Enums;
using PaymentSys.Models.Ledger;

namespace PaymentSys.Data
{
    public static class LedgerAccountSeeder
    {
        public static async Task SeedAsync(
            PaymentDbContext dbContext,
            CancellationToken cancellationToken = default)
        {
            var accounts = new[]
            {
                new Account
                {
                    Name = "Settlement",
                    AccountNumber = "1000",
                    Type = AccountType.Asset,
                    CurrencyCode = "GHS"
                },
                new Account
                {
                    Name = "Revenue",
                    AccountNumber = "4000",
                    Type = AccountType.Revenue,
                    CurrencyCode = "GHS"
                }
            };

            var existingNumbers = await dbContext.Accounts
                .Select(a => a.AccountNumber)
                .ToListAsync(cancellationToken);

            var missingAccounts = accounts
                .Where(a => !existingNumbers.Contains(a.AccountNumber))
                .ToList();

            if (missingAccounts.Count == 0)
            {
                return;
            }

            dbContext.Accounts.AddRange(missingAccounts);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
