namespace PaymentSys.Services.Ledger
{
    public class JournalEntryLineRequest
    {
        public int AccountId { get; set; }

        public long DebitPesewas { get; set; }

        public long CreditPesewas { get; set; }
    }
}
