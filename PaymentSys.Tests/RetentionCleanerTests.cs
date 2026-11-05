using Microsoft.EntityFrameworkCore;
using PaymentSys.Models.Idempotency;
using PaymentSys.Models.Payments;
using PaymentSys.Options;
using PaymentSys.Services.Idempotency;
using PaymentSys.Tests.TestHelpers;

namespace PaymentSys.Tests
{
    public class RetentionCleanerTests
    {
        [Fact]
        public async Task CleanupAsync_RemovesExpiredRequestsAndOldProcessedWebhooks()
        {
            using var dbContext = TestDb.Create();
            var now = DateTime.UtcNow;

            dbContext.IdempotentRequests.AddRange(
                new IdempotentRequest
                {
                    IdempotencyKey = "expired",
                    Endpoint = "anonymous|/api/payments",
                    RequestHash = "hash",
                    ExpiresAt = now.AddHours(-1)
                },
                new IdempotentRequest
                {
                    IdempotencyKey = "fresh",
                    Endpoint = "anonymous|/api/payments",
                    RequestHash = "hash",
                    ExpiresAt = now.AddHours(1)
                });

            dbContext.WebhookLogs.AddRange(
                new WebhookLog
                {
                    EventId = "old-processed",
                    EventType = "charge.success",
                    Payload = "{}",
                    IsProcessed = true,
                    ReceivedAt = now.AddDays(-40)
                },
                new WebhookLog
                {
                    EventId = "old-unprocessed",
                    EventType = "charge.success",
                    Payload = "{}",
                    IsProcessed = false,
                    ReceivedAt = now.AddDays(-40)
                },
                new WebhookLog
                {
                    EventId = "recent-processed",
                    EventType = "charge.success",
                    Payload = "{}",
                    IsProcessed = true,
                    ReceivedAt = now.AddDays(-1)
                });

            await dbContext.SaveChangesAsync();

            var result = await RetentionCleaner.CleanupAsync(
                dbContext,
                new IdempotencyOptions { WebhookRetentionDays = 30 });

            Assert.Equal(1, result.ExpiredIdempotencyRecords);
            Assert.Equal(1, result.ProcessedWebhookLogs);

            Assert.Equal("fresh", (await dbContext.IdempotentRequests.SingleAsync()).IdempotencyKey);

            var remainingWebhooks = await dbContext.WebhookLogs
                .Select(w => w.EventId)
                .ToListAsync();

            Assert.Equal(2, remainingWebhooks.Count);
            Assert.DoesNotContain("old-processed", remainingWebhooks);
            Assert.Contains("old-unprocessed", remainingWebhooks);
            Assert.Contains("recent-processed", remainingWebhooks);
        }
    }
}
