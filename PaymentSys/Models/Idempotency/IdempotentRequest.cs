using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PaymentSys.Models.Enums;

namespace PaymentSys.Models.Idempotency
{
    public class IdempotentRequest
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string IdempotencyKey { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Endpoint { get; set; } = string.Empty;

        [Required, MaxLength(64)]
        public string RequestHash { get; set; } = string.Empty;

        public IdempotencyStatus Status { get; set; } = IdempotencyStatus.InProgress;

        public int? ResponseStatusCode { get; set; }

        public string? ResponseBody { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
