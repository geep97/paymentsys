namespace PaymentSys.Options
{
    public class IdempotencyOptions
    {
        public const string SectionName = "Idempotency";

        public string HeaderName { get; set; } = "Idempotency-Key";

        public int ExpirationHours { get; set; } = 24;

        public int MaxKeyLength { get; set; } = 100;
    }
}
