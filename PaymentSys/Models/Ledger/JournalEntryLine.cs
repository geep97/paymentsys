using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentSys.Models.Ledger
{
    public class JournalEntryLine
    {
        public int Id { get; set; }

        public int JournalEntryId { get; set; }

        [ForeignKey(nameof(JournalEntryId))]
        public JournalEntry JournalEntry { get; set; } = null!;

        public int AccountId { get; set; }

        [ForeignKey(nameof(AccountId))]
        public Account Account { get; set; } = null!;

        public long DebitPesewas { get; set; }

        public long CreditPesewas { get; set; }
    }
}
