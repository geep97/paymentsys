using PaymentSys.Models.Idempotency;

namespace PaymentSys.Services.Idempotency
{
    public interface IIdempotencyService
    {
        Task<IdempotentRequest?> FindAsync(
            string endpoint,
            string idempotencyKey,
            CancellationToken cancellationToken = default);

        Task<IdempotentRequest> BeginAsync(
            string endpoint,
            string idempotencyKey,
            string requestHash,
            CancellationToken cancellationToken = default);

        Task CompleteAsync(
            IdempotentRequest request,
            int statusCode,
            string responseBody,
            CancellationToken cancellationToken = default);

        Task AbandonAsync(
            IdempotentRequest request,
            CancellationToken cancellationToken = default);

        string ComputeRequestHash(string body);
    }
}
