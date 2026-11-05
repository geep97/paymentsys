namespace PaymentSys.Options
{
    public class RateLimitOptions
    {
        public const string SectionName = "RateLimit";

        public int PermitLimit { get; set; } = 60;

        public int WindowSeconds { get; set; } = 60;
    }
}
