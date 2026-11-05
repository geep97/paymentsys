using System.Net;
using System.Text;

namespace PaymentSys.Tests.Integration
{
    public class WebhookRateLimitTests
    {
        [Fact]
        public async Task Webhook_ExceedingRateLimit_ReturnsTooManyRequests()
        {
            await using var factory = new PaymentApiFactory(webhookPermitLimit: 2);
            await factory.InitializeAsync();

            var client = factory.CreateClient();

            for (var attempt = 0; attempt < 2; attempt++)
            {
                var response = await SendWebhookAsync(client);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            var limited = await SendWebhookAsync(client);

            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        }

        private static async Task<HttpResponseMessage> SendWebhookAsync(HttpClient client)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/paystack")
            {
                Content = new StringContent(
                    "{\"event\":\"charge.success\",\"data\":{\"id\":1,\"reference\":\"x\"}}",
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Add("x-paystack-signature", "deadbeef");

            return await client.SendAsync(request);
        }
    }
}
