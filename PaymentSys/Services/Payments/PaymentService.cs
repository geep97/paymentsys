using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentSys.Data;
using PaymentSys.Models.Enums;
using PaymentSys.Models.Ledger;
using PaymentSys.Models.Payments;
using PaymentSys.Options;
using PaymentSys.Services.Ledger;
using PaymentSys.Services.Paystack;

namespace PaymentSys.Services.Payments
{
    public class PaymentService : IPaymentService
    {
        private const string ProviderName = "Paystack";
        private const string PrimaryCurrency = "GHS";

        private readonly PaymentDbContext _dbContext;
        private readonly IPaystackClient _paystackClient;
        private readonly ILedgerPostingService _ledgerPostingService;
        private readonly LedgerOptions _ledgerOptions;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            PaymentDbContext dbContext,
            IPaystackClient paystackClient,
            ILedgerPostingService ledgerPostingService,
            IOptions<LedgerOptions> ledgerOptions,
            ILogger<PaymentService> logger)
        {
            _dbContext = dbContext;
            _paystackClient = paystackClient;
            _ledgerPostingService = ledgerPostingService;
            _ledgerOptions = ledgerOptions.Value;
            _logger = logger;
        }

        public async Task<PaymentInitiationResult> InitiateAsync(
            InitiatePaymentRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var email = NormalizeEmail(request.Email);

            if (request.AmountPesewas <= 0)
            {
                throw new ArgumentException("Amount must be greater than zero.", nameof(request));
            }

            var transaction = new PaymentTransaction
            {
                Reference = GenerateReference(),
                Email = email,
                AmountPesewas = request.AmountPesewas,
                Currency = PrimaryCurrency,
                Status = PaymentStatus.Pending,
                Provider = ProviderName
            };

            _dbContext.PaymentTransactions.Add(transaction);
            await _dbContext.SaveChangesAsync(cancellationToken);

            try
            {
                var response = await _paystackClient.InitializeTransactionAsync(
                    new PaystackInitializeRequest
                    {
                        Email = transaction.Email,
                        Amount = transaction.AmountPesewas,
                        Currency = transaction.Currency,
                        Reference = transaction.Reference
                    },
                    cancellationToken);

                var data = response.Data
                    ?? throw new PaystackException("Paystack did not return initialization data.");

                transaction.AccessCode = data.AccessCode;
                transaction.GatewayResponse = response.Message;
                transaction.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Initiated payment {Reference} for {AmountPesewas} pesewas",
                    transaction.Reference,
                    transaction.AmountPesewas);

                return new PaymentInitiationResult(transaction, data.AuthorizationUrl);
            }
            catch (Exception ex) when (ex is PaystackException
                || ex is HttpRequestException
                || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                _logger.LogError(ex, "Failed to initiate payment {Reference}", transaction.Reference);

                transaction.Status = PaymentStatus.Failed;
                transaction.GatewayResponse = ex.Message;
                transaction.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync(CancellationToken.None);

                throw;
            }
        }

        public async Task<PaymentTransaction> VerifyAsync(
            string reference,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new ArgumentException("Reference is required.", nameof(reference));
            }

            var normalizedReference = reference.Trim();

            var transaction = await _dbContext.PaymentTransactions
                .FirstOrDefaultAsync(p => p.Reference == normalizedReference, cancellationToken)
                ?? throw new KeyNotFoundException($"Payment transaction '{normalizedReference}' was not found.");

            if (transaction.Status == PaymentStatus.Success)
            {
                await EnsureLedgerPostedAsync(transaction, cancellationToken);
                return transaction;
            }

            var response = await _paystackClient.VerifyTransactionAsync(
                transaction.Reference,
                cancellationToken);

            var data = response.Data
                ?? throw new PaystackException(
                    $"Paystack did not return verification data for '{transaction.Reference}'.");

            ApplyProviderStatus(transaction, data);

            transaction.GatewayResponse = data.GatewayResponse ?? response.Message;
            transaction.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction.Status == PaymentStatus.Success)
            {
                await EnsureLedgerPostedAsync(transaction, cancellationToken);

                _logger.LogInformation(
                    "Verified payment {Reference} as successful",
                    transaction.Reference);
            }

            return transaction;
        }

        public async Task<PaymentTransaction> GetAsync(
            string reference,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new ArgumentException("Reference is required.", nameof(reference));
            }

            var normalizedReference = reference.Trim();

            return await _dbContext.PaymentTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.Reference == normalizedReference,
                    cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"Payment transaction '{normalizedReference}' was not found.");
        }

        private static void ApplyProviderStatus(
            PaymentTransaction transaction,
            PaystackTransactionData data)
        {
            var providerStatus = data.Status.Trim().ToLowerInvariant();

            switch (providerStatus)
            {
                case "success":
                    EnsureVerifiedAmountMatches(transaction, data);
                    transaction.Status = PaymentStatus.Success;
                    transaction.PaidAt ??= data.PaidAt?.UtcDateTime ?? DateTime.UtcNow;
                    break;

                case "failed":
                case "abandoned":
                    transaction.Status = PaymentStatus.Failed;
                    break;

                case "reversed":
                    transaction.Status = PaymentStatus.Reversed;
                    break;
            }
        }

        private static void EnsureVerifiedAmountMatches(
            PaymentTransaction transaction,
            PaystackTransactionData data)
        {
            if (data.Amount != transaction.AmountPesewas)
            {
                throw new InvalidOperationException(
                    $"Verified amount {data.Amount} does not match expected amount {transaction.AmountPesewas} " +
                    $"for payment '{transaction.Reference}'.");
            }

            if (!string.Equals(data.Currency, transaction.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Verified currency {data.Currency} does not match expected currency {transaction.Currency} " +
                    $"for payment '{transaction.Reference}'.");
            }
        }

        private async Task EnsureLedgerPostedAsync(
            PaymentTransaction payment,
            CancellationToken cancellationToken)
        {
            var alreadyPosted = await _dbContext.JournalEntries
                .AnyAsync(e => e.PaymentTransactionId == payment.Id, cancellationToken);

            if (alreadyPosted)
            {
                return;
            }

            var settlementAccount = await GetLedgerAccountAsync(
                _ledgerOptions.SettlementAccountNumber,
                "settlement",
                cancellationToken);

            var revenueAccount = await GetLedgerAccountAsync(
                _ledgerOptions.RevenueAccountNumber,
                "revenue",
                cancellationToken);

            await _ledgerPostingService.PostAsync(
                new PostJournalEntryRequest
                {
                    Reference = $"JE-{payment.Reference}",
                    Narration = $"Payment received via {payment.Provider ?? ProviderName} ({payment.Reference})",
                    CurrencyCode = payment.Currency,
                    PaymentTransactionId = payment.Id,
                    Lines = new List<JournalEntryLineRequest>
                    {
                        new()
                        {
                            AccountId = settlementAccount.Id,
                            DebitPesewas = payment.AmountPesewas
                        },
                        new()
                        {
                            AccountId = revenueAccount.Id,
                            CreditPesewas = payment.AmountPesewas
                        }
                    }
                },
                cancellationToken);

            _logger.LogInformation(
                "Posted ledger entry for payment {Reference}",
                payment.Reference);
        }

        private async Task<Account> GetLedgerAccountAsync(
            string accountNumber,
            string role,
            CancellationToken cancellationToken)
        {
            var account = await _dbContext.Accounts
                .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber, cancellationToken);

            if (account is null)
            {
                throw new InvalidOperationException(
                    $"Ledger {role} account '{accountNumber}' is not configured.");
            }

            if (!account.IsActive)
            {
                throw new InvalidOperationException(
                    $"Ledger {role} account '{accountNumber}' is inactive.");
            }

            return account;
        }

        private static string NormalizeEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email is required.", nameof(email));
            }

            var normalized = email.Trim();

            if (!MailAddress.TryCreate(normalized, out var parsed)
                || !string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Email is invalid.", nameof(email));
            }

            return normalized;
        }

        private static string GenerateReference()
        {
            return $"PAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        }
    }
}
