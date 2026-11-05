using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentSys.Data;
using PaymentSys.Services.Ledger;
using PaymentSys.Tests.TestHelpers;

namespace PaymentSys.Tests
{
    public class LedgerPostingServiceTests
    {
        [Fact]
        public async Task PostAsync_CreatesBalancedEntry()
        {
            using var dbContext = TestDb.Create();
            var (settlement, revenue) = await TestDb.SeedAccountsAsync(dbContext);
            var service = CreateService(dbContext);

            var entry = await service.PostAsync(CreateRequest(
                "JE-1",
                settlement.Id,
                revenue.Id,
                1500));

            Assert.Equal("JE-1", entry.Reference);
            Assert.NotNull(entry.PostedAt);
            Assert.Equal(2, entry.Lines.Count);
            Assert.Equal(1500, await dbContext.JournalEntryLines.SumAsync(l => l.DebitPesewas));
            Assert.Equal(1500, await dbContext.JournalEntryLines.SumAsync(l => l.CreditPesewas));
        }

        [Fact]
        public async Task PostAsync_RejectsUnbalancedEntry()
        {
            using var dbContext = TestDb.Create();
            var (settlement, revenue) = await TestDb.SeedAccountsAsync(dbContext);
            var service = CreateService(dbContext);

            var request = new PostJournalEntryRequest
            {
                Reference = "JE-UNBALANCED",
                Narration = "Unbalanced",
                Lines = new List<JournalEntryLineRequest>
                {
                    new() { AccountId = settlement.Id, DebitPesewas = 1500 },
                    new() { AccountId = revenue.Id, CreditPesewas = 1000 }
                }
            };

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => service.PostAsync(request));

            Assert.Contains("not balanced", exception.Message);
            Assert.Equal(0, await dbContext.JournalEntries.CountAsync());
        }

        [Fact]
        public async Task PostAsync_RejectsLineWithBothSides()
        {
            using var dbContext = TestDb.Create();
            var (settlement, revenue) = await TestDb.SeedAccountsAsync(dbContext);
            var service = CreateService(dbContext);

            var request = new PostJournalEntryRequest
            {
                Reference = "JE-BOTH",
                Narration = "Both sides",
                Lines = new List<JournalEntryLineRequest>
                {
                    new() { AccountId = settlement.Id, DebitPesewas = 1500, CreditPesewas = 1500 },
                    new() { AccountId = revenue.Id, CreditPesewas = 1500 }
                }
            };

            await Assert.ThrowsAsync<ArgumentException>(() => service.PostAsync(request));
        }

        [Fact]
        public async Task PostAsync_RejectsFewerThanTwoLines()
        {
            using var dbContext = TestDb.Create();
            var (settlement, _) = await TestDb.SeedAccountsAsync(dbContext);
            var service = CreateService(dbContext);

            var request = new PostJournalEntryRequest
            {
                Reference = "JE-SINGLE",
                Narration = "Single line",
                Lines = new List<JournalEntryLineRequest>
                {
                    new() { AccountId = settlement.Id, DebitPesewas = 1500 }
                }
            };

            await Assert.ThrowsAsync<ArgumentException>(() => service.PostAsync(request));
        }

        [Fact]
        public async Task PostAsync_RejectsInactiveAccount()
        {
            using var dbContext = TestDb.Create();
            var (settlement, revenue) = await TestDb.SeedAccountsAsync(dbContext);
            settlement.IsActive = false;
            await dbContext.SaveChangesAsync();
            var service = CreateService(dbContext);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.PostAsync(CreateRequest("JE-INACTIVE", settlement.Id, revenue.Id, 1500)));

            Assert.Contains("inactive", exception.Message);
        }

        [Fact]
        public async Task PostAsync_RejectsDuplicateReference()
        {
            using var dbContext = TestDb.Create();
            var (settlement, revenue) = await TestDb.SeedAccountsAsync(dbContext);
            var service = CreateService(dbContext);
            var request = CreateRequest("JE-DUPLICATE", settlement.Id, revenue.Id, 1500);

            await service.PostAsync(request);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.PostAsync(request));

            Assert.Contains("already exists", exception.Message);
        }

        [Fact]
        public async Task ReverseAsync_SwapsDebitAndCredit()
        {
            using var dbContext = TestDb.Create();
            var (settlement, revenue) = await TestDb.SeedAccountsAsync(dbContext);
            var service = CreateService(dbContext);

            var original = await service.PostAsync(
                CreateRequest("JE-ORIGINAL", settlement.Id, revenue.Id, 1500));

            var reversal = await service.ReverseAsync(
                original.Id,
                new ReverseJournalEntryRequest
                {
                    Reference = "JE-REVERSAL",
                    Narration = "Reversal"
                });

            Assert.Equal(original.Id, reversal.ReversalOfId);
            Assert.Equal(original.Lines.Sum(l => l.DebitPesewas), reversal.Lines.Sum(l => l.CreditPesewas));
            Assert.Equal(original.Lines.Sum(l => l.CreditPesewas), reversal.Lines.Sum(l => l.DebitPesewas));
        }

        [Fact]
        public async Task ReverseAsync_RejectsSecondReversal()
        {
            using var dbContext = TestDb.Create();
            var (settlement, revenue) = await TestDb.SeedAccountsAsync(dbContext);
            var service = CreateService(dbContext);

            var original = await service.PostAsync(
                CreateRequest("JE-ORIGINAL-2", settlement.Id, revenue.Id, 1500));

            await service.ReverseAsync(original.Id, new ReverseJournalEntryRequest
            {
                Reference = "JE-REVERSAL-2",
                Narration = "Reversal"
            });

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ReverseAsync(original.Id, new ReverseJournalEntryRequest
                {
                    Reference = "JE-REVERSAL-3",
                    Narration = "Second reversal"
                }));

            Assert.Contains("already been reversed", exception.Message);
        }

        private static LedgerPostingService CreateService(PaymentDbContext dbContext)
        {
            return new LedgerPostingService(
                dbContext,
                NullLogger<LedgerPostingService>.Instance);
        }

        private static PostJournalEntryRequest CreateRequest(
            string reference,
            int debitAccountId,
            int creditAccountId,
            long amountPesewas)
        {
            return new PostJournalEntryRequest
            {
                Reference = reference,
                Narration = "Test entry",
                CurrencyCode = "GHS",
                Lines = new List<JournalEntryLineRequest>
                {
                    new() { AccountId = debitAccountId, DebitPesewas = amountPesewas },
                    new() { AccountId = creditAccountId, CreditPesewas = amountPesewas }
                }
            };
        }
    }
}
