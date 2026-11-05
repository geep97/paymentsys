using Microsoft.Extensions.Options;
using PaymentSys.Data;
using PaymentSys.Options;
using PaymentSys.Services.Idempotency;

namespace PaymentSys.BackgroundServices
{
    public sealed class RetentionCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<IdempotencyOptions> _options;
        private readonly ILogger<RetentionCleanupService> _logger;

        public RetentionCleanupService(
            IServiceScopeFactory scopeFactory,
            IOptions<IdempotencyOptions> options,
            ILogger<RetentionCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalMinutes = _options.Value.CleanupIntervalMinutes > 0
                ? _options.Value.CleanupIntervalMinutes
                : 60;

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

                    var result = await RetentionCleaner.CleanupAsync(
                        dbContext,
                        _options.Value,
                        stoppingToken);

                    if (result.ExpiredIdempotencyRecords > 0 || result.ProcessedWebhookLogs > 0)
                    {
                        _logger.LogInformation(
                            "Retention cleanup removed {IdempotencyRecords} idempotency records and {WebhookLogs} webhook logs",
                            result.ExpiredIdempotencyRecords,
                            result.ProcessedWebhookLogs);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Retention cleanup failed");
                }
            }
        }
    }
}
