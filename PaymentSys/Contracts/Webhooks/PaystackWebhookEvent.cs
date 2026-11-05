using System.Text.Json.Serialization;

namespace PaymentSys.Contracts.Webhooks
{
    public class PaystackWebhookEvent
    {
        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public PaystackWebhookData? Data { get; set; }
    }

    public class PaystackWebhookData
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("reference")]
        public string? Reference { get; set; }
    }
}
