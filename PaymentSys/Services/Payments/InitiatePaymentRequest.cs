namespace PaymentSys.Services.Payments
{
    public class InitiatePaymentRequest
    {
        public string Email { get; set; } = string.Empty;

        public long AmountPesewas { get; set; }
    }
}
