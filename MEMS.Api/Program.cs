using MEMS.Data;
using MEMS.Data.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

// Identity stores are needed here for seeding users/roles (and for API login in Phase 3a).
builder.Services.AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

// The API owns the schema: apply migrations and seed before accepting requests.
await SeedData.InitializeAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No HTTPS redirection: in development the gateway calls the API over HTTP inside the Aspire network.
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
