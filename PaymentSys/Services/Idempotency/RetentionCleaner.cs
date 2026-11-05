using Microsoft.EntityFrameworkCore;
using PaymentSys.Data;
using PaymentSys.Options;

namespace PaymentSys.Services.Idempotency
{
    public static class RetentionCleaner
    {
        public static async Task<RetentionCleanupResult> CleanupAsync(
            PaymentDbContext dbContext,
            IdempotencyOptions options,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var expiredRequests = await dbContext.IdempotentRequests
                .Where(r => r.ExpiresAt <= now)
                .ToListAsync(cancellationToken);

            var webhookRetentionDays = options.WebhookRetentionDays > 0
                ? options.WebhookRetentionDays
                : 30;
            var webhookCutoff = now.AddDays(-webhookRetentionDays);

            var oldWebhookLogs = await dbContext.WebhookLogs
                .Where(w => w.IsProcessed && w.ReceivedAt <= webhookCutoff)
                .ToListAsync(cancellationToken);

            if (expiredRequests.Count > 0)
            {
                dbContext.IdempotentRequests.RemoveRange(expiredRequests);
            }

            if (oldWebhookLogs.Count > 0)
            {
                dbContext.WebhookLogs.RemoveRange(oldWebhookLogs);
            }

            if (expiredRequests.Count > 0 || oldWebhookLogs.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return new RetentionCleanupResult(expiredRequests.Count, oldWebhookLogs.Count);
        }
    }

    public record RetentionCleanupResult(
        int ExpiredIdempotencyRecords,
        int ProcessedWebhookLogs);
}
