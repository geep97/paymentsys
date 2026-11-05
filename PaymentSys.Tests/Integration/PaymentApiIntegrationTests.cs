using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PaymentSys.Contracts.Payments;
using PaymentSys.Models.Enums;
using PaymentSys.Services.Payments;
using PaymentSys.Services.Paystack;

namespace PaymentSys.Tests.Integration
{
    public class PaymentApiIntegrationTests : IClassFixture<PaymentApiFactory>
    {
        private readonly PaymentApiFactory _factory;

        public PaymentApiIntegrationTests(PaymentApiFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Initiate_WithoutApiKey_ReturnsUnauthorized()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                "/api/payments",
                new InitiatePaymentRequest
                {
                    Email = "buyer@example.com",
                    AmountPesewas = 1000
                });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetPayment_WithoutApiKey_ReturnsUnauthorized()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/payments/any-reference");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Initiate_WithoutIdempotencyKey_ReturnsBadRequest()
        {
            var client = CreateAuthorizedClient();

            var response = await client.PostAsJsonAsync(
                "/api/payments",
                new InitiatePaymentRequest
                {
                    Email = "buyer@example.com",
                    AmountPesewas = 1000
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Initiate_WithValidRequest_ReturnsCreatedAndReplaysResponse()
        {
            var client = CreateAuthorizedClient();
            var idempotencyKey = $"integration-{Guid.NewGuid():N}";
            var body = JsonSerializer.Serialize(new InitiatePaymentRequest
            {
                Email = "buyer@example.com",
                AmountPesewas = 2500
            });

            var initializeCallsBefore = _factory.Paystack.InitializeCallCount;

            var firstResponse = await SendIdempotentAsync(client, idempotencyKey, body);

            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

            var created = await firstResponse.Content.ReadFromJsonAsync<PaymentResponse>();

            Assert.NotNull(created);
            Assert.False(string.IsNullOrWhiteSpace(created.Reference));
            Assert.Equal(2500, created.AmountPesewas);
            Assert.Equal(PaymentStatus.Pending.ToString(), created.Status);
            Assert.Equal("https://checkout.paystack.com/test", created.AuthorizationUrl);

            var replayResponse = await SendIdempotentAsync(client, idempotencyKey, body);

            Assert.Equal(HttpStatusCode.Created, replayResponse.StatusCode);

            var replayed = await replayResponse.Content.ReadFromJsonAsync<PaymentResponse>();

            Assert.NotNull(replayed);
            Assert.Equal(created.Reference, replayed.Reference);
            Assert.Equal(initializeCallsBefore + 1, _factory.Paystack.InitializeCallCount);
        }

        [Fact]
        public async Task GetPayment_WithUnknownReference_ReturnsNotFound()
        {
            var client = CreateAuthorizedClient();

            var response = await client.GetAsync($"/api/payments/missing-{Guid.NewGuid():N}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Webhook_WithInvalidSignature_ReturnsUnauthorized()
        {
            var client = _factory.CreateClient();
            var payload = """{"event":"charge.success","data":{"id":1,"reference":"unknown"}}""";

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/paystack")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-paystack-signature", "deadbeef");

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Webhook_ChargeSuccess_MarksPaymentSuccessfulAndIgnoresDuplicates()
        {
            var client = CreateAuthorizedClient();
            var payment = await InitiateAsync(client, 1500);

            _factory.Paystack.VerifyResponse = CreateSuccessVerifyResponse(1500);

            var verifyCallsBefore = _factory.Paystack.VerifyCallCount;
            var eventId = Random.Shared.Next(100000, 999999);
            var payload = JsonSerializer.Serialize(new
            {
                @event = "charge.success",
                data = new { id = eventId, reference = payment.Reference }
            });

            var firstResponse = await SendSignedWebhookAsync(client, payload);
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

            var duplicateResponse = await SendSignedWebhookAsync(client, payload);
            Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);

            Assert.Equal(verifyCallsBefore + 1, _factory.Paystack.VerifyCallCount);

            var updated = await GetPaymentAsync(client, payment.Reference);
            Assert.Equal(PaymentStatus.Success.ToString(), updated.Status);
        }

        [Fact]
        public async Task Webhook_RefundProcessed_ReversesSuccessfulPayment()
        {
            var client = CreateAuthorizedClient();
            var payment = await InitiateAsync(client, 1200);

            _factory.Paystack.VerifyResponse = CreateSuccessVerifyResponse(1200);

            var verifyResponse = await client.PostAsync(
                $"/api/payments/{payment.Reference}/verify",
                content: null);

            Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

            var payload = JsonSerializer.Serialize(new
            {
                @event = "refund.processed",
                data = new
                {
                    id = Random.Shared.Next(100000, 999999),
                    reference = payment.Reference
                }
            });

            var webhookResponse = await SendSignedWebhookAsync(client, payload);
            Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

            var updated = await GetPaymentAsync(client, payment.Reference);
            Assert.Equal(PaymentStatus.Reversed.ToString(), updated.Status);
        }

        [Fact]
        public async Task Responses_IncludeSecurityHeaders()
        {
            var client = _factory.CreateClient();

            var health = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
            Assert.Equal("nosniff", health.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", health.Headers.GetValues("X-Frame-Options").Single());
            Assert.True(health.Headers.CacheControl?.NoStore);

            var api = await client.GetAsync("/api/payments/any-reference");

            Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
            Assert.Equal("nosniff", api.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.True(api.Headers.CacheControl?.NoStore);
        }

        private HttpClient CreateAuthorizedClient()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", PaymentApiFactory.ApiKey);

            return client;
        }

        private static async Task<HttpResponseMessage> SendIdempotentAsync(
            HttpClient client,
            string idempotencyKey,
            string body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Idempotency-Key", idempotencyKey);

            return await client.SendAsync(request);
        }

        private static async Task<PaymentResponse> InitiateAsync(
            HttpClient client,
            long amountPesewas)
        {
            var body = JsonSerializer.Serialize(new InitiatePaymentRequest
            {
                Email = "buyer@example.com",
                AmountPesewas = amountPesewas
            });

            var response = await SendIdempotentAsync(
                client,
                $"integration-{Guid.NewGuid():N}",
                body);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();

            Assert.NotNull(payment);

            return payment;
        }

        private static async Task<PaymentResponse> GetPaymentAsync(
            HttpClient client,
            string reference)
        {
            var response = await client.GetAsync($"/api/payments/{reference}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>();

            Assert.NotNull(payment);

            return payment;
        }

        private static async Task<HttpResponseMessage> SendSignedWebhookAsync(
            HttpClient client,
            string payload)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/paystack")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add(
                "x-paystack-signature",
                SignPayload(payload, PaymentApiFactory.SecretKey));

            return await client.SendAsync(request);
        }

        private static string SignPayload(string payload, string secretKey)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secretKey));

            return Convert.ToHexString(
                hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        }

        private static PaystackVerifyResponse CreateSuccessVerifyResponse(long amountPesewas)
        {
            return new PaystackVerifyResponse
            {
                Status = true,
                Message = "Verification successful",
                Data = new PaystackTransactionData
                {
                    Id = Random.Shared.Next(100000, 999999),
                    Status = "success",
                    Amount = amountPesewas,
                    Currency = "GHS",
                    PaidAt = DateTimeOffset.UtcNow,
                    GatewayResponse = "Successful",
                    Fees = 0
                }
            };
        }
    }
}
