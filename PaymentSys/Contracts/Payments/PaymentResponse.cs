using PaymentSys.Models.Payments;

namespace PaymentSys.Contracts.Payments
{
    public class PaymentResponse
    {
        public string Reference { get; init; } = string.Empty;

        public long AmountPesewas { get; init; }

        public string Currency { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public string? Provider { get; init; }

        public string? AuthorizationUrl { get; init; }

        public DateTime CreatedAt { get; init; }

        public DateTime? PaidAt { get; init; }

        public static PaymentResponse From(
            PaymentTransaction transaction,
            string? authorizationUrl = null)
        {
            return new PaymentResponse
            {
                Reference = transaction.Reference,
                AmountPesewas = transaction.AmountPesewas,
                Currency = transaction.Currency,
                Status = transaction.Status.ToString(),
                Provider = transaction.Provider,
                AuthorizationUrl = authorizationUrl,
                CreatedAt = transaction.CreatedAt,
                PaidAt = transaction.PaidAt
            };
        }
    }
}
