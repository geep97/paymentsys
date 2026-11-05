using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentSys.Data;
using PaymentSys.Models.Enums;
using PaymentSys.Models.Idempotency;
using PaymentSys.Options;

namespace PaymentSys.Services.Idempotency
{
    public class IdempotencyService : IIdempotencyService
    {
        private readonly PaymentDbContext _dbContext;
        private readonly IdempotencyOptions _options;

        public IdempotencyService(
            PaymentDbContext dbContext,
            IOptions<IdempotencyOptions> options)
        {
            _dbContext = dbContext;
            _options = options.Value;
        }

        public Task<IdempotentRequest?> FindAsync(
            string endpoint,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return _dbContext.IdempotentRequests
                .FirstOrDefaultAsync(
                    r => r.Endpoint == endpoint && r.IdempotencyKey == idempotencyKey,
                    cancellationToken);
        }

        public async Task<IdempotentRequest> BeginAsync(
            string endpoint,
            string idempotencyKey,
            string requestHash,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var expirationHours = _options.ExpirationHours > 0 ? _options.ExpirationHours : 24;

            var request = new IdempotentRequest
            {
                Endpoint = endpoint,
                IdempotencyKey = idempotencyKey,
                RequestHash = requestHash,
                Status = IdempotencyStatus.InProgress,
                CreatedAt = now,
                ExpiresAt = now.AddHours(expirationHours)
            };

            _dbContext.IdempotentRequests.Add(request);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return request;
        }

        public async Task CompleteAsync(
            IdempotentRequest request,
            int statusCode,
            string responseBody,
            CancellationToken cancellationToken = default)
        {
            request.Status = IdempotencyStatus.Completed;
            request.ResponseStatusCode = statusCode;
            request.ResponseBody = responseBody;
            request.CompletedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task AbandonAsync(
            IdempotentRequest request,
            CancellationToken cancellationToken = default)
        {
            _dbContext.IdempotentRequests.Remove(request);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public string ComputeRequestHash(string body)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(body ?? string.Empty));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
