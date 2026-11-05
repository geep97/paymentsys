using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using PaymentSys.Middleware;

namespace PaymentSys.OpenApi
{
    public sealed class PaymentOperationTransformer : IOpenApiOperationTransformer
    {
        private const string IdempotencyHeaderName = "Idempotency-Key";

        public Task TransformAsync(
            OpenApiOperation operation,
            OpenApiOperationTransformerContext context,
            CancellationToken cancellationToken)
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;

            if (metadata.OfType<IAllowAnonymous>().Any())
            {
                operation.Security = new List<OpenApiSecurityRequirement>();
            }

            if (metadata.OfType<IdempotentAttribute>().Any())
            {
                operation.Parameters ??= new List<IOpenApiParameter>();

                var hasIdempotencyHeader = operation.Parameters.Any(parameter =>
                    parameter.In == ParameterLocation.Header
                    && string.Equals(parameter.Name, IdempotencyHeaderName, StringComparison.OrdinalIgnoreCase));

                if (!hasIdempotencyHeader)
                {
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = IdempotencyHeaderName,
                        In = ParameterLocation.Header,
                        Required = true,
                        Description = "Unique key that makes retries of this money-moving request safe.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                    });
                }
            }

            return Task.CompletedTask;
        }
    }
}
