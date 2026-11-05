using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PaymentSys.Options;

namespace PaymentSys.Services.Paystack
{
    public class PaystackSignatureVerifier : IPaystackSignatureVerifier
    {
        private readonly PaystackOptions _options;

        public PaystackSignatureVerifier(IOptions<PaystackOptions> options)
        {
            _options = options.Value;
        }

        public bool IsValid(string payload, string signature)
        {
            if (string.IsNullOrWhiteSpace(_options.SecretKey)
                || string.IsNullOrWhiteSpace(signature))
            {
                return false;
            }

            byte[] providedSignature;

            try
            {
                providedSignature = Convert.FromHexString(signature.Trim());
            }
            catch (FormatException)
            {
                return false;
            }

            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_options.SecretKey));
            var computedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

            return CryptographicOperations.FixedTimeEquals(computedSignature, providedSignature);
        }
    }
}
