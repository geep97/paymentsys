using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using PaymentSys.Data;
using PaymentSys.Models.Enums;
using PaymentSys.Options;
using PaymentSys.Services.Ledger;
using PaymentSys.Services.Payments;
using PaymentSys.Tests.TestHelpers;

namespace PaymentSys.Tests
{
    public class PaymentServiceTests
    {
        [Fact]
        public async Task InitiateAsync_CreatesPendingTransactionAndCallsProvider()
        {
            using var dbContext = TestDb.Create();
            var fakeClient = new FakePaystackClient();
            var service = CreateService(dbContext, fakeClient);

            var result = await service.InitiateAsync(new InitiatePaymentRequest
            {
                Email = "customer@example.com",
                AmountPesewas = 1500
            });

            Assert.Equal(PaymentStatus.Pending, result.Transaction.Status);
            Assert.Equal("https://checkout.paystack.com/test", result.AuthorizationUrl);
            Assert.Equal(1, fakeClient.InitializeCallCount);
            Assert.Equal(1500, fakeClient.LastInitializeRequest!.Amount);
            Assert.Equal("GHS", fakeClient.LastInitializeRequest.Currency);
            Assert.Single(dbContext.PaymentTransactions);
        }

        [Fact]
        public async Task VerifyAsync_OnSuccessPostsBalancedLedgerEntryOnce()
        {
            using var dbContext = TestDb.Create();
            await TestDb.SeedAccountsAsync(dbContext);
            var fakeClient = new FakePaystackClient();
            var service = CreateService(dbContext, fakeClient);

            var initiated = await service.InitiateAsync(new InitiatePaymentRequest
            {
                Email = "customer@example.com",
                AmountPesewas = 1500
            });

            ConfigureVerifyResponse(fakeClient, "success", 1500);

            var verified = await service.VerifyAsync(initiated.Transaction.Reference);

            Assert.Equal(PaymentStatus.Success, verified.Status);
            Assert.NotNull(verified.PaidAt);

            var entry = await dbContext.JournalEntries
                .Include(e => e.Lines)
                .SingleAsync(e => e.PaymentTransactionId == verified.Id);

            Assert.Equal(1500, entry.Lines.Sum(l => l.DebitPesewas));
            Assert.Equal(1500, entry.Lines.Sum(l => l.CreditPesewas));

            await service.VerifyAsync(initiated.Transaction.Reference);

            Assert.Equal(1, await dbContext.JournalEntries.CountAsync());
            Assert.Equal(1, fakeClient.VerifyCallCount);
        }

        [Fact]
        public async Task VerifyAsync_OnFailureMarksTransactionFailedWithoutLedgerEntry()
        {
            using var dbContext = TestDb.Create();
            await TestDb.SeedAccountsAsync(dbContext);
            var fakeClient = new FakePaystackClient();
            var service = CreateService(dbContext, fakeClient);

            var initiated = await service.InitiateAsync(new InitiatePaymentRequest
            {
                Email = "customer@example.com",
                AmountPesewas = 1500
            });

            ConfigureVerifyResponse(fakeClient, "failed", 1500);

            var verified = await service.VerifyAsync(initiated.Transaction.Reference);

            Assert.Equal(PaymentStatus.Failed, verified.Status);
            Assert.Empty(dbContext.JournalEntries);
        }

        [Fact]
        public async Task VerifyAsync_RejectsAmountMismatch()
        {
            using var dbContext = TestDb.Create();
            await TestDb.SeedAccountsAsync(dbContext);
            var fakeClient = new FakePaystackClient();
            var service = CreateService(dbContext, fakeClient);

            var initiated = await service.InitiateAsync(new InitiatePaymentRequest
            {
                Email = "customer@example.com",
                AmountPesewas = 1500
            });

            ConfigureVerifyResponse(fakeClient, "success", 999);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.VerifyAsync(initiated.Transaction.Reference));

            Assert.Equal(PaymentStatus.Pending, initiated.Transaction.Status);
            Assert.Empty(dbContext.JournalEntries);
        }

        [Fact]
        public async Task InitiateAsync_RejectsInvalidEmail()
        {
            using var dbContext = TestDb.Create();
            var service = CreateService(dbContext, new FakePaystackClient());

            await Assert.ThrowsAsync<ArgumentException>(
                () => service.InitiateAsync(new InitiatePaymentRequest
                {
                    Email = "not-an-email",
                    AmountPesewas = 1500
                }));
        }

        private static void ConfigureVerifyResponse(
            FakePaystackClient fakeClient,
            string status,
            long amountPesewas)
        {
            fakeClient.VerifyResponse.Data!.Status = status;
            fakeClient.VerifyResponse.Data.Amount = amountPesewas;
            fakeClient.VerifyResponse.Data.Currency = "GHS";
            fakeClient.VerifyResponse.Data.PaidAt = status == "success"
                ? DateTimeOffset.UtcNow
                : null;
        }

        private static PaymentService CreateService(
            PaymentDbContext dbContext,
            FakePaystackClient fakeClient)
        {
            var ledgerService = new LedgerPostingService(
                dbContext,
                NullLogger<LedgerPostingService>.Instance);

            return new PaymentService(
                dbContext,
                fakeClient,
                ledgerService,
                OptionsFactory.Create(new LedgerOptions()),
                NullLogger<PaymentService>.Instance);
        }
    }
}
