using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MEMS.Api.Infrastructure;

/// <summary>Document title/description and the Bearer (JWT) scheme so Swagger UI has an "Authorize" button.</summary>
internal sealed class OpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "MEMS API",
            Version = "v1",
            Description =
                "Tenant-scoped API for the Municipal Elections Management System. Every tenant-owned route starts with " +
                "/api/{tenant}/ (e.g. /api/burnaby/candidates) and only ever returns that municipality's data. " +
                "GET endpoints are public. POST/PUT/DELETE need a JWT from POST /api/auth/login; a Municipality Admin's " +
                "token only works for their own municipality (403 otherwise). Errors are RFC 7807 ProblemDetails.",
        };

        // Relative server URL: "Try it out" calls whatever host served the page (the gateway).
        document.Servers = [new OpenApiServer { Url = "/" }];

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the token from POST /api/auth/login.",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        });

        return Task.CompletedTask;
    }
}
