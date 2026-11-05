using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentSys.Models.Enums;
using PaymentSys.Models.Idempotency;
using PaymentSys.Options;
using PaymentSys.Services.Idempotency;

namespace PaymentSys.Middleware
{
    public class IdempotencyMiddleware
    {
        private const string ReplayContentType = "application/json; charset=utf-8";

        private readonly RequestDelegate _next;
        private readonly ILogger<IdempotencyMiddleware> _logger;

        public IdempotencyMiddleware(
            RequestDelegate next,
            ILogger<IdempotencyMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IIdempotencyService idempotencyService,
            IOptions<IdempotencyOptions> options)
        {
            var isIdempotent = context.GetEndpoint()?.Metadata.GetMetadata<IdempotentAttribute>() is not null;

            if (!isIdempotent || !HttpMethods.IsPost(context.Request.Method))
            {
                await _next(context);
                return;
            }

            var settings = options.Value;

            if (!context.Request.Headers.TryGetValue(settings.HeaderName, out var headerValues)
                || string.IsNullOrWhiteSpace(headerValues.ToString()))
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    $"The {settings.HeaderName} header is required.");
                return;
            }

            var idempotencyKey = headerValues.ToString().Trim();

            if (idempotencyKey.Length > settings.MaxKeyLength)
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    $"The {settings.HeaderName} header cannot exceed {settings.MaxKeyLength} characters.");
                return;
            }

            var endpoint = BuildEndpoint(context);

            var requestHash = await ComputeRequestHashAsync(context);

            var existing = await idempotencyService.FindAsync(
                endpoint,
                idempotencyKey,
                context.RequestAborted);

            if (existing is not null)
            {
                var now = DateTime.UtcNow;
                var timedOut = existing.Status == IdempotencyStatus.InProgress
                    && existing.CreatedAt <= now.AddMinutes(-GetInProgressTimeout(settings));

                if (existing.ExpiresAt <= now || timedOut)
                {
                    try
                    {
                        await idempotencyService.AbandonAsync(existing, context.RequestAborted);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        // Another request already cleared this record; proceed as new.
                    }
                }
                else if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    await WriteProblemAsync(
                        context,
                        StatusCodes.Status422UnprocessableEntity,
                        "This Idempotency-Key was already used with a different request payload.");
                    return;
                }
                else if (existing.Status == IdempotencyStatus.InProgress)
                {
                    await WriteProblemAsync(
                        context,
                        StatusCodes.Status409Conflict,
                        "A request with this Idempotency-Key is already in progress.");
                    return;
                }
                else
                {
                    _logger.LogInformation(
                        "Replaying idempotent response for {Endpoint}",
                        endpoint);

                    context.Response.StatusCode = existing.ResponseStatusCode
                        ?? StatusCodes.Status200OK;
                    context.Response.ContentType = ReplayContentType;

                    if (!string.IsNullOrEmpty(existing.ResponseBody))
                    {
                        await context.Response.WriteAsync(existing.ResponseBody);
                    }

                    return;
                }
            }

            IdempotentRequest record;

            try
            {
                record = await idempotencyService.BeginAsync(
                    endpoint,
                    idempotencyKey,
                    requestHash,
                    context.RequestAborted);
            }
            catch (DbUpdateException)
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status409Conflict,
                    "A concurrent request with this Idempotency-Key is already in progress.");
                return;
            }

            var originalResponseBody = context.Response.Body;
            await using var responseBuffer = new MemoryStream();
            context.Response.Body = responseBuffer;

            try
            {
                await _next(context);

                responseBuffer.Position = 0;
                var responseBody = await new StreamReader(
                    responseBuffer,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    leaveOpen: true).ReadToEndAsync();

                await idempotencyService.CompleteAsync(
                    record,
                    context.Response.StatusCode,
                    responseBody,
                    CancellationToken.None);

                responseBuffer.Position = 0;
                await responseBuffer.CopyToAsync(originalResponseBody);
            }
            catch
            {
                try
                {
                    await idempotencyService.AbandonAsync(record, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogError(
                        cleanupException,
                        "Failed to abandon idempotency record for {Endpoint}",
                        endpoint);
                }

                throw;
            }
            finally
            {
                context.Response.Body = originalResponseBody;
            }
        }

        private static string BuildEndpoint(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "/";
            var client = context.User.Identity?.Name;

            var endpoint = string.IsNullOrWhiteSpace(client)
                ? $"anonymous|{path}"
                : $"{client}|{path}";

            if (endpoint.Length <= 200)
            {
                return endpoint;
            }

            var hash = System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(endpoint));

            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static double GetInProgressTimeout(IdempotencyOptions settings)
        {
            return settings.InProgressTimeoutMinutes > 0
                ? settings.InProgressTimeoutMinutes
                : 5;
        }

        private static async Task<string> ComputeRequestHashAsync(HttpContext context)
        {
            context.Request.EnableBuffering();

            string body;

            using (var reader = new StreamReader(
                context.Request.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true))
            {
                body = await reader.ReadToEndAsync(context.RequestAborted);
            }

            context.Request.Body.Position = 0;

            var hash = System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(body));

            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static async Task WriteProblemAsync(
            HttpContext context,
            int statusCode,
            string detail)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = "Idempotency error",
                Detail = detail
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
