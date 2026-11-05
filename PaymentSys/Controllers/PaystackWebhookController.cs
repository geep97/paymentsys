using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentSys.Contracts.Webhooks;
using PaymentSys.Data;
using PaymentSys.Models.Payments;
using PaymentSys.Services.Payments;
using PaymentSys.Services.Paystack;

namespace PaymentSys.Controllers
{
    [ApiController]
    [Route("api/webhooks/paystack")]
    [AllowAnonymous]
    public class PaystackWebhookController : ControllerBase
    {
        private const string ProviderName = "Paystack";
        private const string SignatureHeader = "x-paystack-signature";
        private const string ChargeSuccessEvent = "charge.success";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly PaymentDbContext _dbContext;
        private readonly IPaystackSignatureVerifier _signatureVerifier;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaystackWebhookController> _logger;

        public PaystackWebhookController(
            PaymentDbContext dbContext,
            IPaystackSignatureVerifier signatureVerifier,
            IPaymentService paymentService,
            ILogger<PaystackWebhookController> logger)
        {
            _dbContext = dbContext;
            _signatureVerifier = signatureVerifier;
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Receive(CancellationToken cancellationToken)
        {
            Request.EnableBuffering();

            string payload;

            using (var reader = new StreamReader(
                Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true))
            {
                payload = await reader.ReadToEndAsync(cancellationToken);
            }

            Request.Body.Position = 0;

            var signature = Request.Headers[SignatureHeader].ToString();

            if (!_signatureVerifier.IsValid(payload, signature))
            {
                _logger.LogWarning("Rejected Paystack webhook with an invalid signature");
                return Unauthorized();
            }

            PaystackWebhookEvent? webhook;

            try
            {
                webhook = JsonSerializer.Deserialize<PaystackWebhookEvent>(payload, JsonOptions);
            }
            catch (JsonException)
            {
                return BadRequest();
            }

            if (webhook is null
                || string.IsNullOrWhiteSpace(webhook.Event)
                || webhook.Data is null)
            {
                return BadRequest();
            }

            if (webhook.Data.Id <= 0 && string.IsNullOrWhiteSpace(webhook.Data.Reference))
            {
                return BadRequest();
            }

            var eventId = webhook.Data.Id > 0
                ? $"{webhook.Event}:{webhook.Data.Id}"
                : $"{webhook.Event}:{webhook.Data.Reference}";

            if (eventId.Length > 100)
            {
                eventId = eventId[..100];
            }

            var alreadyReceived = await _dbContext.WebhookLogs
                .AnyAsync(
                    w => w.Provider == ProviderName && w.EventId == eventId,
                    cancellationToken);

            if (alreadyReceived)
            {
                _logger.LogInformation("Ignoring duplicate Paystack webhook {EventId}", eventId);
                return Ok();
            }

            var log = new WebhookLog
            {
                EventId = eventId,
                EventType = webhook.Event,
                Provider = ProviderName,
                Payload = payload
            };

            _dbContext.WebhookLogs.Add(log);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _logger.LogInformation("Concurrent Paystack webhook {EventId} was already received", eventId);
                return Ok();
            }

            try
            {
                if (string.Equals(webhook.Event, ChargeSuccessEvent, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(webhook.Data.Reference))
                    {
                        throw new InvalidOperationException(
                            "Paystack webhook is missing the transaction reference.");
                    }

                    await _paymentService.VerifyAsync(webhook.Data.Reference, cancellationToken);
                }

                log.IsProcessed = true;
                log.ProcessedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process Paystack webhook {EventId}", eventId);

                log.ProcessingAttempts++;
                log.ErrorMessage = ex.Message.Length > 1000
                    ? ex.Message[..1000]
                    : ex.Message;

                await _dbContext.SaveChangesAsync(CancellationToken.None);

                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
