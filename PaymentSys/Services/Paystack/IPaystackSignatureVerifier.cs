namespace PaymentSys.Services.Paystack
{
    public interface IPaystackSignatureVerifier
    {
        bool IsValid(string payload, string signature);
    }
}
