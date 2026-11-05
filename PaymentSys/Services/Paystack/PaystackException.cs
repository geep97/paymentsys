namespace PaymentSys.Services.Paystack
{
    public class PaystackException : Exception
    {
        public PaystackException(string message)
            : base(message)
        {
        }

        public PaystackException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
