using PaymentSys.Models.Payments;

namespace PaymentSys.Services.Payments
{
    public interface IPaymentService
    {
        Task<PaymentInitiationResult> InitiateAsync(
            InitiatePaymentRequest request,
            CancellationToken cancellationToken = default);

        Task<PaymentTransaction> VerifyAsync(
            string reference,
            CancellationToken cancellationToken = default);

        Task<PaymentTransaction> GetAsync(
            string reference,
            CancellationToken cancellationToken = default);

        Task<PaymentTransaction> RefundAsync(
            string reference,
            RefundPaymentRequest request,
            CancellationToken cancellationToken = default);

        Task<PaymentTransaction> MarkReversedAsync(
            string reference,
            CancellationToken cancellationToken = default);
    }
}
