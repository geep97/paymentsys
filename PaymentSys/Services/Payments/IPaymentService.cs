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
    }
}
