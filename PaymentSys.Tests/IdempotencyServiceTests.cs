using Microsoft.Extensions.Options;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using PaymentSys.Data;
using PaymentSys.Models.Enums;
using PaymentSys.Options;
using PaymentSys.Services.Idempotency;
using PaymentSys.Tests.TestHelpers;

namespace PaymentSys.Tests
{
    public class IdempotencyServiceTests
    {
        [Fact]
        public async Task BeginAsync_CreatesInProgressRequest()
        {
            using var dbContext = TestDb.Create();
            var service = CreateService(dbContext);

            var record = await service.BeginAsync(
                "/api/payments",
                "key-1",
                service.ComputeRequestHash("{}"));

            Assert.Equal(IdempotencyStatus.InProgress, record.Status);
            Assert.True(record.ExpiresAt > record.CreatedAt);
            Assert.Equal(64, record.RequestHash.Length);
        }

        [Fact]
        public async Task CompleteAsync_StoresResponseAndMarksCompleted()
        {
            using var dbContext = TestDb.Create();
            var service = CreateService(dbContext);

            var record = await service.BeginAsync(
                "/api/payments",
                "key-2",
                service.ComputeRequestHash("{}"));

            await service.CompleteAsync(record, 201, "{\"reference\":\"PAY-1\"}");

            var stored = await service.FindAsync("/api/payments", "key-2");

            Assert.NotNull(stored);
            Assert.Equal(IdempotencyStatus.Completed, stored.Status);
            Assert.Equal(201, stored.ResponseStatusCode);
            Assert.Equal("{\"reference\":\"PAY-1\"}", stored.ResponseBody);
            Assert.NotNull(stored.CompletedAt);
        }

        [Fact]
        public async Task AbandonAsync_RemovesRequest()
        {
            using var dbContext = TestDb.Create();
            var service = CreateService(dbContext);

            var record = await service.BeginAsync(
                "/api/payments",
                "key-3",
                service.ComputeRequestHash("{}"));

            await service.AbandonAsync(record);

            Assert.Null(await service.FindAsync("/api/payments", "key-3"));
        }

        [Fact]
        public async Task ComputeRequestHash_IsDeterministicAndPayloadSensitive()
        {
            using var dbContext = TestDb.Create();
            var service = CreateService(dbContext);

            var first = service.ComputeRequestHash("{\"amount\":100}");
            var second = service.ComputeRequestHash("{\"amount\":100}");
            var different = service.ComputeRequestHash("{\"amount\":200}");

            Assert.Equal(first, second);
            Assert.NotEqual(first, different);
            Assert.Equal(64, first.Length);
        }

        private static IdempotencyService CreateService(PaymentDbContext dbContext)
        {
            return new IdempotencyService(
                dbContext,
                OptionsFactory.Create(new IdempotencyOptions()));
        }
    }
}
