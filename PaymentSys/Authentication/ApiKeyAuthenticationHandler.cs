using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PaymentSys.Options;

namespace PaymentSys.Authentication
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "ApiKey";
        public const string HeaderName = "X-Api-Key";

        private readonly MerchantOptions _merchantOptions;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IOptions<MerchantOptions> merchantOptions)
            : base(options, logger, encoder)
        {
            _merchantOptions = merchantOptions.Value;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(HeaderName, out var providedKey)
                || string.IsNullOrWhiteSpace(providedKey))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (string.IsNullOrWhiteSpace(_merchantOptions.ApiKey))
            {
                return Task.FromResult(AuthenticateResult.Fail("Merchant API key is not configured."));
            }

            if (!KeysMatch(providedKey.ToString(), _merchantOptions.ApiKey))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, _merchantOptions.ClientId)
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        private static bool KeysMatch(string providedKey, string expectedKey)
        {
            var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
            var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedKey));

            return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
        }
    }
}
