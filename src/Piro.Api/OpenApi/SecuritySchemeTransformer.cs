using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Piro.Api.OpenApi;

/// <summary>
/// Adds JWT Bearer and API Key security schemes to the OpenAPI document.
/// </summary>
internal sealed class SecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        // JWT Bearer
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT access token obtained from `POST /api/v1/auth/sign-in`.",
        };

        // API Key (X-API-Key header)
        document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-API-Key",
            Description = "API key for programmatic access. Prefix: `pk_`.",
        };

        // Global security requirements (OR semantics — either scheme is accepted)
        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        });
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("ApiKey", document)] = [],
        });

        return Task.CompletedTask;
    }
}
