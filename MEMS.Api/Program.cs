using MEMS;
using MEMS.Api.Auth;
using MEMS.Api.Infrastructure;
using MEMS.Api.Tenancy;
using MEMS.Data;
using MEMS.Data.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Aspire: OpenTelemetry logging/tracing/metrics, health checks, service discovery, resilient HttpClients.
builder.AddServiceDefaults();

// Connection string "memsdb" is injected by the AppHost (WithReference(db)); nothing is stored in appsettings.
// AddDbContext (not the pooled AddSqlServerDbContext) because AppDbContext takes the scoped tenant context.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("memsdb")
        ?? throw new InvalidOperationException("Connection string 'memsdb' not found. Run the solution through MEMS.AppHost.")));

// Aspire adds retries, the DbContext health check and EF Core tracing to the registration above.
builder.EnrichSqlServerDbContext<AppDbContext>();

builder.Services.AddMemsData();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

// JWT bearer auth. The signing key comes from the AppHost's generated "jwt-key" secret parameter.
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
if (jwt.Key.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters. It is supplied by MEMS.AppHost.");
}

builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep "sub", "name", "role", "tenant_id" as issued
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.SigningKey,
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = JwtSettings.RoleClaim,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

// Every controller action runs through the tenant filter (route slug -> tenant context, cross-tenant 403).
builder.Services.AddControllers(options => options.Filters.Add<TenantContextFilter>());

// Errors: RFC 7807 ProblemDetails everywhere, no stack traces or SQL errors in responses.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DbUpdateConflictHandler>();

builder.Services.AddOpenApi(options => options.AddDocumentTransformer<OpenApiDocumentTransformer>());

var app = builder.Build();

// The API owns the schema: apply migrations and seed before accepting requests.
await SeedData.InitializeAsync(app.Services);

app.UseExceptionHandler();
app.UseStatusCodePages(); // empty 401/403/404 responses get a ProblemDetails body too

app.Use((context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    return next();
});

// API docs under /api so they are reachable through the gateway: /api/swagger and /api/openapi/v1.json
app.MapOpenApi("/api/openapi/{documentName}.json");
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/api/openapi/v1.json", "MEMS API v1");
    options.RoutePrefix = "api/swagger";
    options.DocumentTitle = "MEMS API";
});

// No HTTPS redirection: in development the gateway calls the API over HTTP inside the Aspire network.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
