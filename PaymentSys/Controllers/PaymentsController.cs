using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PaymentSys.Contracts.Payments;
using PaymentSys.Middleware;
using PaymentSys.Services.Payments;

namespace PaymentSys.Controllers
{
    [ApiController]
    [Authorize]
    [EnableRateLimiting("payments")]
    [Route("api/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost]
        [Idempotent]
        [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
        public async Task<ActionResult<PaymentResponse>> Initiate(
            [FromBody] InitiatePaymentRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _paymentService.InitiateAsync(request, cancellationToken);
            var response = PaymentResponse.From(result.Transaction, result.AuthorizationUrl);

            return CreatedAtAction(
                nameof(GetByReference),
                new { reference = result.Transaction.Reference },
                response);
        }

        [HttpGet("{reference}")]
        [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<PaymentResponse>> GetByReference(
            string reference,
            CancellationToken cancellationToken)
        {
            var transaction = await _paymentService.GetAsync(reference, cancellationToken);

            return Ok(PaymentResponse.From(transaction));
        }

        [HttpPost("{reference}/verify")]
        [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<PaymentResponse>> Verify(
            string reference,
            CancellationToken cancellationToken)
        {
            var transaction = await _paymentService.VerifyAsync(reference, cancellationToken);

            return Ok(PaymentResponse.From(transaction));
        }

        [HttpPost("{reference}/refund")]
        [Idempotent]
        [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
        public async Task<ActionResult<PaymentResponse>> Refund(
            string reference,
            [FromBody] RefundPaymentRequest request,
            CancellationToken cancellationToken)
        {
            var transaction = await _paymentService.RefundAsync(reference, request, cancellationToken);

            return Ok(PaymentResponse.From(transaction));
        }
    }
}
