namespace PaymentSys.Options
{
    public class PaystackOptions
    {
        public const string SectionName = "Paystack";

        public string SecretKey { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = "https://api.paystack.co";

        public string? CallbackUrl { get; set; }

        public int TimeoutSeconds { get; set; } = 30;
    }
}
