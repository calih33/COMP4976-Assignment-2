using System.Security.Claims;
using MEMS.Data.Services;
using MEMS.Data.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace MEMS.Api.Tenancy;

/// <summary>
/// Runs before every controller action (after authentication/authorization) and fills the scoped
/// <see cref="TenantContext"/>, which drives the EF Core query filters and the service-layer checks.
/// <list type="bullet">
/// <item>Route has <c>{tenant}</c>: resolve the slug (404 if unknown). Anonymous callers get read-only access to that
/// tenant. A Municipality Admin whose token is for a different tenant gets 403 before the action runs.</item>
/// <item>No <c>{tenant}</c> (e.g. <c>/api/municipalities</c>): admins get their own scope from the token.</item>
/// </list>
/// </summary>
public sealed class TenantContextFilter(
    TenantContext tenant,
    IMunicipalityService municipalities,
    ProblemDetailsFactory problems,
    ILogger<TenantContextFilter> logger) : IAsyncResourceFilter
{
    public const string SlugItemKey = "TenantSlug";

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var user = http.User;

        var isSuperAdmin = user.IsInRole(AppRoles.SuperAdmin);
        var isAdmin = isSuperAdmin || user.IsInRole(AppRoles.MunicipalityAdmin);
        int? userTenantId = int.TryParse(user.FindFirstValue(AppRoles.TenantClaim), out var id) ? id : null;

        if (context.RouteData.Values.TryGetValue("tenant", out var raw) && raw is string slug)
        {
            var municipality = await municipalities.FindBySlugAsync(slug, http.RequestAborted);
            if (municipality is null)
            {
                context.Result = Problem(http, StatusCodes.Status404NotFound, "Municipality not found.",
                    $"No municipality uses the URL segment '{slug}'.");
                return;
            }

            if (isAdmin && !isSuperAdmin && userTenantId != municipality.MunicipalityId)
            {
                logger.LogWarning("Admin {UserName} (tenant {UserTenantId}) was denied access to tenant {Slug}",
                    user.Identity?.Name, userTenantId, municipality.Slug);
                context.Result = Problem(http, StatusCodes.Status403Forbidden, "Access to this municipality is not allowed.",
                    "Municipality Administrators can only access their own municipality.");
                return;
            }

            if (isAdmin)
            {
                tenant.SetAdmin(municipality.MunicipalityId, isSuperAdmin);
            }
            else
            {
                tenant.SetPublic(municipality.MunicipalityId);
            }

            http.Items[SlugItemKey] = municipality.Slug;
        }
        else if (isAdmin)
        {
            tenant.SetAdmin(isSuperAdmin ? null : userTenantId, isSuperAdmin);
        }

        await next();
    }

    private ObjectResult Problem(HttpContext http, int status, string title, string detail) =>
        new(problems.CreateProblemDetails(http, status, title, detail: detail)) { StatusCode = status };
}
