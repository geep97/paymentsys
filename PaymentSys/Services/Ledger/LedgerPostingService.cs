using Microsoft.EntityFrameworkCore;
using PaymentSys.Data;
using PaymentSys.Models.Ledger;

namespace PaymentSys.Services.Ledger
{
    public class LedgerPostingService : ILedgerPostingService
    {
        private const string PrimaryCurrency = "GHS";

        private readonly PaymentDbContext _dbContext;
        private readonly ILogger<LedgerPostingService> _logger;

        public LedgerPostingService(
            PaymentDbContext dbContext,
            ILogger<LedgerPostingService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<JournalEntry> PostAsync(
            PostJournalEntryRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateRequest(request.Reference, request.Narration, request.CurrencyCode, request.Lines);

            var reference = request.Reference.Trim();
            var currencyCode = NormalizeCurrency(request.CurrencyCode);

            await EnsureReferenceIsUniqueAsync(reference, cancellationToken);
            await EnsurePaymentTransactionExistsAsync(request.PaymentTransactionId, cancellationToken);
            _ = await LoadAccountsAsync(request.Lines, currencyCode, cancellationToken);

            await using var ownedTransaction = _dbContext.Database.CurrentTransaction is null
                ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;

            var entry = new JournalEntry
            {
                Reference = reference,
                Narration = request.Narration.Trim(),
                CurrencyCode = currencyCode,
                PaymentTransactionId = request.PaymentTransactionId,
                CreatedAt = DateTime.UtcNow,
                PostedAt = DateTime.UtcNow
            };

            foreach (var line in request.Lines)
            {
                entry.Lines.Add(new JournalEntryLine
                {
                    AccountId = line.AccountId,
                    DebitPesewas = line.DebitPesewas,
                    CreditPesewas = line.CreditPesewas
                });
            }

            _dbContext.JournalEntries.Add(entry);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);

                if (ownedTransaction is not null)
                {
                    await ownedTransaction.CommitAsync(cancellationToken);
                }
            }
            catch
            {
                if (ownedTransaction is not null)
                {
                    await ownedTransaction.RollbackAsync(CancellationToken.None);
                }

                throw;
            }

            _logger.LogInformation(
                "Posted journal entry {Reference} with {LineCount} lines",
                entry.Reference,
                entry.Lines.Count);

            return entry;
        }

        public async Task<JournalEntry> ReverseAsync(
            int journalEntryId,
            ReverseJournalEntryRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateReference(request.Reference);
            ValidateNarration(request.Narration);

            var reference = request.Reference.Trim();

            await EnsureReferenceIsUniqueAsync(reference, cancellationToken);

            var original = await _dbContext.JournalEntries
                .Include(e => e.Lines)
                .FirstOrDefaultAsync(e => e.Id == journalEntryId, cancellationToken)
                ?? throw new KeyNotFoundException($"Journal entry {journalEntryId} was not found.");

            if (original.PostedAt is null)
            {
                throw new InvalidOperationException($"Journal entry {journalEntryId} has not been posted.");
            }

            if (original.ReversalOfId is not null)
            {
                throw new InvalidOperationException("A reversal journal entry cannot be reversed.");
            }

            var alreadyReversed = await _dbContext.JournalEntries
                .AnyAsync(e => e.ReversalOfId == journalEntryId, cancellationToken);

            if (alreadyReversed)
            {
                throw new InvalidOperationException($"Journal entry {journalEntryId} has already been reversed.");
            }

            await using var ownedTransaction = _dbContext.Database.CurrentTransaction is null
                ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;

            var reversal = new JournalEntry
            {
                Reference = reference,
                Narration = request.Narration.Trim(),
                CurrencyCode = original.CurrencyCode,
                PaymentTransactionId = original.PaymentTransactionId,
                ReversalOfId = original.Id,
                CreatedAt = DateTime.UtcNow,
                PostedAt = DateTime.UtcNow
            };

            foreach (var line in original.Lines)
            {
                reversal.Lines.Add(new JournalEntryLine
                {
                    AccountId = line.AccountId,
                    DebitPesewas = line.CreditPesewas,
                    CreditPesewas = line.DebitPesewas
                });
            }

            _dbContext.JournalEntries.Add(reversal);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);

                if (ownedTransaction is not null)
                {
                    await ownedTransaction.CommitAsync(cancellationToken);
                }
            }
            catch
            {
                if (ownedTransaction is not null)
                {
                    await ownedTransaction.RollbackAsync(CancellationToken.None);
                }

                throw;
            }

            _logger.LogInformation(
                "Reversed journal entry {OriginalId} with journal entry {ReversalId}",
                original.Id,
                reversal.Id);

            return reversal;
        }

        private static void ValidateRequest(
            string reference,
            string narration,
            string currencyCode,
            IReadOnlyCollection<JournalEntryLineRequest>? lines)
        {
            ValidateReference(reference);
            ValidateNarration(narration);

            _ = NormalizeCurrency(currencyCode);

            if (lines is null || lines.Count < 2)
            {
                throw new ArgumentException("A journal entry must contain at least two lines.", nameof(lines));
            }

            foreach (var line in lines)
            {
                if (line.DebitPesewas < 0 || line.CreditPesewas < 0)
                {
                    throw new ArgumentException("Journal entry line amounts cannot be negative.", nameof(lines));
                }

                var hasDebit = line.DebitPesewas > 0;
                var hasCredit = line.CreditPesewas > 0;

                if (hasDebit == hasCredit)
                {
                    throw new ArgumentException(
                        "Each journal entry line must contain exactly one non-zero debit or credit amount.",
                        nameof(lines));
                }
            }

            var totalDebits = lines.Sum(l => (decimal)l.DebitPesewas);
            var totalCredits = lines.Sum(l => (decimal)l.CreditPesewas);

            if (totalDebits != totalCredits)
            {
                throw new ArgumentException(
                    $"Journal entry is not balanced. Debits ({totalDebits}) must equal credits ({totalCredits}).",
                    nameof(lines));
            }

            if (totalDebits == 0)
            {
                throw new ArgumentException("A journal entry must move a non-zero amount.", nameof(lines));
            }
        }

        private static void ValidateReference(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new ArgumentException("Reference is required.", nameof(reference));
            }

            if (reference.Trim().Length > 100)
            {
                throw new ArgumentException("Reference cannot exceed 100 characters.", nameof(reference));
            }
        }

        private static void ValidateNarration(string narration)
        {
            if (string.IsNullOrWhiteSpace(narration))
            {
                throw new ArgumentException("Narration is required.", nameof(narration));
            }

            if (narration.Trim().Length > 255)
            {
                throw new ArgumentException("Narration cannot exceed 255 characters.", nameof(narration));
            }
        }

        private static string NormalizeCurrency(string currencyCode)
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
            {
                throw new ArgumentException("Currency code is required.", nameof(currencyCode));
            }

            var normalized = currencyCode.Trim().ToUpperInvariant();

            if (normalized.Length != 3)
            {
                throw new ArgumentException("Currency code must be three characters.", nameof(currencyCode));
            }

            if (normalized != PrimaryCurrency)
            {
                throw new ArgumentException($"Only {PrimaryCurrency} is supported.", nameof(currencyCode));
            }

            return normalized;
        }

        private async Task EnsureReferenceIsUniqueAsync(string reference, CancellationToken cancellationToken)
        {
            var exists = await _dbContext.JournalEntries
                .AnyAsync(e => e.Reference == reference, cancellationToken);

            if (exists)
            {
                throw new InvalidOperationException($"A journal entry with reference '{reference}' already exists.");
            }
        }

        private async Task EnsurePaymentTransactionExistsAsync(
            int? paymentTransactionId,
            CancellationToken cancellationToken)
        {
            if (paymentTransactionId is null)
            {
                return;
            }

            var exists = await _dbContext.PaymentTransactions
                .AnyAsync(p => p.Id == paymentTransactionId, cancellationToken);

            if (!exists)
            {
                throw new KeyNotFoundException($"Payment transaction {paymentTransactionId} was not found.");
            }
        }

        private async Task<Dictionary<int, Account>> LoadAccountsAsync(
            IReadOnlyCollection<JournalEntryLineRequest> lines,
            string currencyCode,
            CancellationToken cancellationToken)
        {
            var accountIds = lines
                .Select(l => l.AccountId)
                .Distinct()
                .ToList();

            var accounts = await _dbContext.Accounts
                .Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken);

            foreach (var accountId in accountIds)
            {
                if (!accounts.TryGetValue(accountId, out var account))
                {
                    throw new KeyNotFoundException($"Account {accountId} was not found.");
                }

                if (!account.IsActive)
                {
                    throw new InvalidOperationException($"Account {accountId} is inactive.");
                }

                if (account.CurrencyCode != currencyCode)
                {
                    throw new InvalidOperationException(
                        $"Account {accountId} is denominated in {account.CurrencyCode}, not {currencyCode}.");
                }
            }

            return accounts;
        }
    }
}
