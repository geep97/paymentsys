namespace PaymentSys.Options
{
    public class IdempotencyOptions
    {
        public const string SectionName = "Idempotency";

        public string HeaderName { get; set; } = "Idempotency-Key";

        public int ExpirationHours { get; set; } = 24;

        public int MaxKeyLength { get; set; } = 100;

        public int InProgressTimeoutMinutes { get; set; } = 5;

        public int CleanupIntervalMinutes { get; set; } = 60;

        public int WebhookRetentionDays { get; set; } = 30;
    }
}
