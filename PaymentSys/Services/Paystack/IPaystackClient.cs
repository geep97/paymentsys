namespace PaymentSys.Services.Paystack
{
    public interface IPaystackClient
    {
        Task<PaystackInitializeResponse> InitializeTransactionAsync(
            PaystackInitializeRequest request,
            CancellationToken cancellationToken = default);

        Task<PaystackVerifyResponse> VerifyTransactionAsync(
            string reference,
            CancellationToken cancellationToken = default);

        Task<PaystackRefundResponse> RefundTransactionAsync(
            PaystackRefundRequest request,
            CancellationToken cancellationToken = default);
    }
}
