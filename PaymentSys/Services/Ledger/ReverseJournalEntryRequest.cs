namespace PaymentSys.Services.Ledger
{
    public class ReverseJournalEntryRequest
    {
        public string Reference { get; set; } = string.Empty;

        public string Narration { get; set; } = string.Empty;
    }
}
