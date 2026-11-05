using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using PaymentSys.Options;
using PaymentSys.Services.Paystack;

namespace PaymentSys.Tests
{
    public class PaystackSignatureVerifierTests
    {
        private const string SecretKey = "sk_test_unit_test_secret";

        [Fact]
        public void IsValid_AcceptsCorrectSignature()
        {
            var verifier = CreateVerifier();
            const string payload = "{\"event\":\"charge.success\"}";

            var signature = Sign(payload, SecretKey);

            Assert.True(verifier.IsValid(payload, signature));
        }

        [Fact]
        public void IsValid_RejectsInvalidSignature()
        {
            var verifier = CreateVerifier();
            const string payload = "{\"event\":\"charge.success\"}";

            Assert.False(verifier.IsValid(payload, Sign(payload, "wrong-secret")));
        }

        [Fact]
        public void IsValid_RejectsMalformedSignature()
        {
            var verifier = CreateVerifier();

            Assert.False(verifier.IsValid("{}", "not-a-hex-signature"));
        }

        [Fact]
        public void IsValid_RejectsEmptySignature()
        {
            var verifier = CreateVerifier();

            Assert.False(verifier.IsValid("{}", string.Empty));
        }

        private static PaystackSignatureVerifier CreateVerifier()
        {
            return new PaystackSignatureVerifier(
                OptionsFactory.Create(new PaystackOptions { SecretKey = SecretKey }));
        }

        private static string Sign(string payload, string secretKey)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
