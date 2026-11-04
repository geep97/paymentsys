using System.ComponentModel.DataAnnotations;

namespace PaymentSys.Models.Payments
{
    public class WebhookLog
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string EventId { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string EventType { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Provider { get; set; } = "Paystack";

        [Required]
        public string Payload { get; set; } = string.Empty;

        public bool IsProcessed { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public int ProcessingAttempts { get; set; }

        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }

        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    }
}
