namespace PaymentSys.Services.Ledger
{
    public class PostJournalEntryRequest
    {
        public string Reference { get; set; } = string.Empty;

        public string Narration { get; set; } = string.Empty;

        public string CurrencyCode { get; set; } = "GHS";

        public int? PaymentTransactionId { get; set; }

        public IReadOnlyCollection<JournalEntryLineRequest> Lines { get; set; } = Array.Empty<JournalEntryLineRequest>();
    }
}
