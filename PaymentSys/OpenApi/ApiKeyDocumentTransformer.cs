using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using PaymentSys.Authentication;

namespace PaymentSys.OpenApi
{
    public sealed class ApiKeyDocumentTransformer : IOpenApiDocumentTransformer
    {
        public const string SchemeName = "ApiKey";

        public Task TransformAsync(
            OpenApiDocument document,
            OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

            document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = ApiKeyAuthenticationHandler.HeaderName,
                Description = "Merchant API key sent in the X-Api-Key header."
            };

            document.Security ??= new List<OpenApiSecurityRequirement>();
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SchemeName, document)] = new List<string>()
            });

            return Task.CompletedTask;
        }
    }
}
