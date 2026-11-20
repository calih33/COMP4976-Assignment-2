using System.Security.Claims;
using MEMS;
using MEMS.Admin.Auth;
using MEMS.Admin.Components;
using MEMS.Data;
using MEMS.Data.Services;
using MEMS.Data.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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

builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AdminClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/Login";

    static Task Relative(RedirectContext<CookieAuthenticationOptions> context)
    {
        var target = Uri.TryCreate(context.RedirectUri, UriKind.Absolute, out var abs) ? abs.PathAndQuery : context.RedirectUri;
        context.Response.Redirect(target);
        return Task.CompletedTask;
    }

    options.Events.OnRedirectToLogin = Relative;
    options.Events.OnRedirectToLogout = Relative;
    options.Events.OnRedirectToAccessDenied = Relative;
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
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();
app.MapRazorPages();

app.MapGet("/images/{id:int}", async (int id, HttpContext http, TenantContext tenant, IImageService images, CancellationToken ct) =>
{
    var isSuperAdmin = http.User.IsInRole(AppRoles.SuperAdmin);
    int? tenantId = int.TryParse(http.User.FindFirstValue(AppRoles.TenantClaim), out var t) ? t : null;
    tenant.SetAdmin(isSuperAdmin ? null : tenantId, isSuperAdmin);

    return await images.GetAsync(id, ct) is { } image
        ? Results.File(image.Data, image.ContentType)
        : Results.NotFound();
}).RequireAuthorization();

app.MapDefaultEndpoints();

app.Run();
