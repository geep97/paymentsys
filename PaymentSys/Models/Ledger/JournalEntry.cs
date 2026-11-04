using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PaymentSys.Models.Payments;

namespace PaymentSys.Models.Ledger
{
    public class JournalEntry
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Reference { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string Narration { get; set; } = string.Empty;

        [Required, MaxLength(3)]
        public string CurrencyCode { get; set; } = "GHS";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? PostedAt { get; set; }

        public int? PaymentTransactionId { get; set; }

        [ForeignKey(nameof(PaymentTransactionId))]
        public PaymentTransaction? PaymentTransaction { get; set; }

        public int? ReversalOfId { get; set; }

        [ForeignKey(nameof(ReversalOfId))]
        public JournalEntry? ReversalOf { get; set; }

        public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
    }
}
