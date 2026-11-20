using MEMS;
using MEMS.Admin.Auth;
using MEMS.Admin.Components;
using MEMS.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("memsdb")
        ?? throw new InvalidOperationException("Connection string 'memsdb' not found. Run the solution through MEMS.AppHost.")));
builder.EnrichSqlServerDbContext<AppDbContext>();

builder.Services.AddMemsData();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddRoles<IdentityRole>()
    .AddDefaultTokenProviders();

// Puts the admin's tenant id on the cookie as the "tenant_id" claim, same claim the API reads from the JWT.
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AdminClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/Login";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRazorPages();

var app = builder.Build();

app.UsePathBase("/admin");
app.UseRouting();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapRazorPages();

app.MapDefaultEndpoints();

app.Run();
