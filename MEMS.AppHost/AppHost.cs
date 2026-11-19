var builder = DistributedApplication.CreateBuilder(args);

// SQL Server in a container. The data volume keeps the database between runs; the generated sa password is
// stored in the AppHost's user secrets. Connection strings flow to projects by resource name ("memsdb").
var sql = builder.AddSqlServer("sql")
    .WithDataVolume("mems-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("memsdb");

// JWT signing key for the API: generated once and kept in the AppHost's user secrets, never committed.
var jwtKey = builder.AddParameter("jwt-key", new GenerateParameterDefault { MinLength = 64, Special = false }, secret: true, persist: true);

// The API applies migrations and seeds on startup, so everything else waits for it.
var api = builder.AddProject<Projects.MEMS_Api>("api")
    .WithReference(db)
    .WithEnvironment("Jwt__Key", jwtKey)
    .WaitFor(db)
    .WithHttpHealthCheck("/health");

var admin = builder.AddProject<Projects.MEMS_Admin>("admin")
    .WithReference(db)
    .WaitFor(api)
    .WithHttpHealthCheck("/health");

// Blazor WebAssembly runs in the browser, so it reaches the API through the gateway (same origin, no CORS).
var publicSite = builder.AddProject<Projects.MEMS_Public>("public")
    .WithReference(api)
    .WaitFor(api);

// Single entry point, matching the assignment's URLs:
//   http://localhost:5001/burnaby      -> public site (tenant from the URL)
//   http://localhost:5001/admin        -> admin site
//   http://localhost:5001/api/burnaby/ -> API
// Routes resolve each project through Aspire service discovery, never a hard-coded port.
builder.AddYarp("gateway")
    .WithHostPort(5001)
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/api/{**catch-all}", api.GetEndpoint("http"));
        yarp.AddRoute("/admin/{**catch-all}", admin.GetEndpoint("http"));
        yarp.AddRoute("/{**catch-all}", publicSite.GetEndpoint("http"));
    })
    .WaitFor(api)
    .WaitFor(admin)
    .WaitFor(publicSite);

builder.Build().Run();
