namespace PaymentSys.Options
{
    public class MerchantOptions
    {
        public const string SectionName = "Merchant";

        public string ApiKey { get; set; } = string.Empty;

        public string ClientId { get; set; } = "default-merchant";
    }
}
