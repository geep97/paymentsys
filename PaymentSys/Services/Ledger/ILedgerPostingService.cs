using PaymentSys.Models.Ledger;

namespace PaymentSys.Services.Ledger
{
    public interface ILedgerPostingService
    {
        Task<JournalEntry> PostAsync(
            PostJournalEntryRequest request,
            CancellationToken cancellationToken = default);

        Task<JournalEntry> ReverseAsync(
            int journalEntryId,
            ReverseJournalEntryRequest request,
            CancellationToken cancellationToken = default);
    }
}
