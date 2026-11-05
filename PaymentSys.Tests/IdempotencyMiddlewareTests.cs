using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using PaymentSys.Data;
using PaymentSys.Middleware;
using PaymentSys.Options;
using PaymentSys.Services.Idempotency;
using PaymentSys.Tests.TestHelpers;

namespace PaymentSys.Tests
{
    public class IdempotencyMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_ReplaysResponseForSameKeyAndBody()
        {
            using var dbContext = TestDb.Create();
            var service = CreateService(dbContext);
            var nextCalls = 0;

            var middleware = new IdempotencyMiddleware(
                async context =>
                {
                    nextCalls++;
                    context.Response.StatusCode = StatusCodes.Status201Created;
                    await context.Response.WriteAsync("created");
                },
                NullLogger<IdempotencyMiddleware>.Instance);

            var firstRequest = CreateContext("key-1", "{\"amount\":100}");
            await middleware.InvokeAsync(firstRequest, service, CreateOptions());

            var secondRequest = CreateContext("key-1", "{\"amount\":100}");
            await middleware.InvokeAsync(secondRequest, service, CreateOptions());

            Assert.Equal(1, nextCalls);
            Assert.Equal(StatusCodes.Status201Created, secondRequest.Response.StatusCode);
            Assert.Equal("created", await ReadBodyAsync(secondRequest));
        }

        [Fact]
        public async Task InvokeAsync_RequiresIdempotencyKey()
        {
            using var dbContext = TestDb.Create();
            var service = CreateService(dbContext);
            var nextCalls = 0;

            var middleware = new IdempotencyMiddleware(
                _ =>
                {
                    nextCalls++;
                    return Task.CompletedTask;
                },
                NullLogger<IdempotencyMiddleware>.Instance);

            var context = CreateContext(null, "{}");
            await middleware.InvokeAsync(context, service, CreateOptions());

            Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
            Assert.Equal(0, nextCalls);
        }

        [Fact]
        public async Task InvokeAsync_RejectsSameKeyWithDifferentPayload()
        {
            using var dbContext = TestDb.Create();
            var service = CreateService(dbContext);

            var middleware = new IdempotencyMiddleware(
                context =>
                {
                    context.Response.StatusCode = StatusCodes.Status201Created;
                    return Task.CompletedTask;
                },
                NullLogger<IdempotencyMiddleware>.Instance);

            var firstRequest = CreateContext("key-2", "{\"amount\":100}");
            await middleware.InvokeAsync(firstRequest, service, CreateOptions());

            var secondRequest = CreateContext("key-2", "{\"amount\":200}");
            await middleware.InvokeAsync(secondRequest, service, CreateOptions());

            Assert.Equal(StatusCodes.Status422UnprocessableEntity, secondRequest.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_ReturnsConflictWhileRequestInProgress()
        {
            using var dbContext = TestDb.Create();
            var service = CreateService(dbContext);

            await service.BeginAsync(
                "/api/payments",
                "key-3",
                service.ComputeRequestHash("{}"));

            var middleware = new IdempotencyMiddleware(
                _ => Task.CompletedTask,
                NullLogger<IdempotencyMiddleware>.Instance);

            var context = CreateContext("key-3", "{}");
            await middleware.InvokeAsync(context, service, CreateOptions());

            Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        }

        private static IdempotencyService CreateService(PaymentDbContext dbContext)
        {
            return new IdempotencyService(
                dbContext,
                OptionsFactory.Create(new IdempotencyOptions()));
        }

        private static IOptions<IdempotencyOptions> CreateOptions()
        {
            return OptionsFactory.Create(new IdempotencyOptions());
        }

        private static DefaultHttpContext CreateContext(string? idempotencyKey, string body)
        {
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Method = HttpMethods.Post,
                    Path = "/api/payments",
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(body))
                },
                Response = { Body = new MemoryStream() }
            };

            if (idempotencyKey is not null)
            {
                context.Request.Headers["Idempotency-Key"] = idempotencyKey;
            }

            context.SetEndpoint(new Endpoint(
                requestDelegate: null,
                new EndpointMetadataCollection(new IdempotentAttribute()),
                "test"));

            return context;
        }

        private static async Task<string> ReadBodyAsync(HttpContext context)
        {
            context.Response.Body.Position = 0;

            using var reader = new StreamReader(
                context.Response.Body,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                leaveOpen: true);

            return await reader.ReadToEndAsync();
        }
    }
}
