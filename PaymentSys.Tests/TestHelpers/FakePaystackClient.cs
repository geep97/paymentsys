using PaymentSys.Services.Paystack;

namespace PaymentSys.Tests.TestHelpers
{
    internal sealed class FakePaystackClient : IPaystackClient
    {
        public PaystackInitializeResponse InitializeResponse { get; set; } = new()
        {
            Status = true,
            Message = "Authorization URL created",
            Data = new PaystackInitializeData
            {
                AuthorizationUrl = "https://checkout.paystack.com/test",
                AccessCode = "test-access-code",
                Reference = "TEST"
            }
        };

        public PaystackVerifyResponse VerifyResponse { get; set; } = new()
        {
            Status = true,
            Message = "Verification successful",
            Data = new PaystackTransactionData
            {
                Id = 1,
                Status = "success",
                Reference = "TEST",
                Amount = 100,
                Currency = "GHS",
                PaidAt = DateTimeOffset.UtcNow,
                GatewayResponse = "Successful"
            }
        };

        public int InitializeCallCount { get; private set; }

        public int VerifyCallCount { get; private set; }

        public PaystackInitializeRequest? LastInitializeRequest { get; private set; }

        public string? LastVerifiedReference { get; private set; }

        public Task<PaystackInitializeResponse> InitializeTransactionAsync(
            PaystackInitializeRequest request,
            CancellationToken cancellationToken = default)
        {
            InitializeCallCount++;
            LastInitializeRequest = request;
            InitializeResponse.Data!.Reference = request.Reference;

            return Task.FromResult(InitializeResponse);
        }

        public Task<PaystackVerifyResponse> VerifyTransactionAsync(
            string reference,
            CancellationToken cancellationToken = default)
        {
            VerifyCallCount++;
            LastVerifiedReference = reference;
            VerifyResponse.Data!.Reference = reference;

            return Task.FromResult(VerifyResponse);
        }
    }
}
