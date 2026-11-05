using PaymentSys.Models.Payments;

namespace PaymentSys.Services.Payments
{
    public record PaymentInitiationResult(PaymentTransaction Transaction, string AuthorizationUrl);
}
