using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CleanArchTemplate.Api.OpenApi;

/// <summary>
/// Declares the bearer scheme on the generated document so the API explorer offers an
/// "Authorize" box instead of leaving callers to craft the header by hand.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);

        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the access token returned by POST /api/v1/auth/login.",
        };

        return Task.CompletedTask;
    }
}
