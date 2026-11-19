using MEMS.Admin.Components;
using MEMS.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("memsdb")
        ?? throw new InvalidOperationException("Connection string 'memsdb' not found. Run the solution through MEMS.AppHost.")));
builder.EnrichSqlServerDbContext<AppDbContext>();

builder.Services.AddMemsData();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// The gateway forwards /admin/** here unchanged; the path base makes links, assets and the Blazor
// SignalR hub live under /admin. Routing must run after UsePathBase.
app.UsePathBase("/admin");
app.UseRouting();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
