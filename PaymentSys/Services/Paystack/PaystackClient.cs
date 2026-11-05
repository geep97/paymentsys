using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PaymentSys.Options;

namespace PaymentSys.Services.Paystack
{
    public class PaystackClient : IPaystackClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _httpClient;
        private readonly PaystackOptions _options;
        private readonly ILogger<PaystackClient> _logger;

        public PaystackClient(
            HttpClient httpClient,
            IOptions<PaystackOptions> options,
            ILogger<PaystackClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;

            if (!string.IsNullOrWhiteSpace(_options.SecretKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _options.SecretKey);
            }
        }

        public async Task<PaystackInitializeResponse> InitializeTransactionAsync(
            PaystackInitializeRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureSecretKeyConfigured();

            request.CallbackUrl = string.IsNullOrWhiteSpace(_options.CallbackUrl)
                ? null
                : _options.CallbackUrl;

            using var response = await _httpClient.PostAsJsonAsync(
                "transaction/initialize",
                request,
                JsonOptions,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            EnsureSuccessfulResponse(response, body, "initialize transaction");

            var result = Deserialize<PaystackInitializeResponse>(body, "initialize transaction");

            if (!result.Status || result.Data is null || string.IsNullOrWhiteSpace(result.Data.AuthorizationUrl))
            {
                throw new PaystackException(
                    $"Paystack rejected the transaction initialization: {result.Message}");
            }

            _logger.LogInformation(
                "Initialized Paystack transaction {Reference}",
                result.Data.Reference);

            return result;
        }

        public async Task<PaystackVerifyResponse> VerifyTransactionAsync(
            string reference,
            CancellationToken cancellationToken = default)
        {
            EnsureSecretKeyConfigured();

            using var response = await _httpClient.GetAsync(
                $"transaction/verify/{Uri.EscapeDataString(reference)}",
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            EnsureSuccessfulResponse(response, body, "verify transaction");

            var result = Deserialize<PaystackVerifyResponse>(body, "verify transaction");

            if (!result.Status || result.Data is null)
            {
                throw new PaystackException(
                    $"Paystack could not verify transaction {reference}: {result.Message}");
            }

            return result;
        }

        public async Task<PaystackRefundResponse> RefundTransactionAsync(
            PaystackRefundRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureSecretKeyConfigured();

            using var response = await _httpClient.PostAsJsonAsync(
                "refund",
                request,
                JsonOptions,
                cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            EnsureSuccessfulResponse(response, body, "refund transaction");

            var result = Deserialize<PaystackRefundResponse>(body, "refund transaction");

            if (!result.Status || result.Data is null)
            {
                throw new PaystackException(
                    $"Paystack rejected the refund: {result.Message}");
            }

            _logger.LogInformation(
                "Requested Paystack refund {RefundId} for transaction {Reference}",
                result.Data.Id,
                request.Transaction);

            return result;
        }

        private void EnsureSecretKeyConfigured()
        {
            if (string.IsNullOrWhiteSpace(_options.SecretKey))
            {
                throw new PaystackException(
                    $"Paystack secret key is not configured. Set '{PaystackOptions.SectionName}:SecretKey'.");
            }
        }

        private void EnsureSuccessfulResponse(
            HttpResponseMessage response,
            string body,
            string operation)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var message = body.Length > 500
                ? body[..500]
                : body;

            _logger.LogWarning(
                "Paystack {Operation} failed with status code {StatusCode}",
                operation,
                (int)response.StatusCode);

            throw new PaystackException(
                $"Paystack {operation} failed with status code {(int)response.StatusCode}: {message}");
        }

        private static T Deserialize<T>(string body, string operation)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(body, JsonOptions)
                    ?? throw new PaystackException(
                        $"Paystack returned an empty response while trying to {operation}.");
            }
            catch (JsonException ex)
            {
                throw new PaystackException(
                    $"Paystack returned an invalid response while trying to {operation}.",
                    ex);
            }
        }
    }
}
