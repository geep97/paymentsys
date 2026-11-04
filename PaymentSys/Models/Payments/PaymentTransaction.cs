using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PaymentSys.Models.Enums;

namespace PaymentSys.Models.Payments
{
    public class PaymentTransaction
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Reference { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? AccessCode { get; set; }

        [Required, EmailAddress, MaxLength(254)]
        public string Email { get; set; } = string.Empty;

        public long AmountPesewas { get; set; }

        [Required, MaxLength(3)]
        public string Currency { get; set; } = "GHS";

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        [MaxLength(50)]
        public string? Provider { get; set; }

        public string? GatewayResponse { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? PaidAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
