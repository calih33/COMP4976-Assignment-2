using System.Security.Claims;
using MEMS;
using MEMS.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MEMS.Admin.Auth;

public sealed class AdminClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.MunicipalityId is int tenantId)
        {
            identity.AddClaim(new Claim(AppRoles.TenantClaim, tenantId.ToString()));
        }

        return identity;
    }
}
