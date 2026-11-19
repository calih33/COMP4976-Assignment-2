namespace MEMS;

public static class AppRoles
{
    /// <summary>Manages every tenant, including creating and deleting municipalities.</summary>
    public const string SuperAdmin = "Super Admin";

    /// <summary>Manages exactly one tenant (<c>ApplicationUser.MunicipalityId</c>).</summary>
    public const string MunicipalityAdmin = "Municipality Admin";

    public const string AnyAdmin = SuperAdmin + "," + MunicipalityAdmin;

    /// <summary>Claim that carries the admin's tenant id (cookie and JWT).</summary>
    public const string TenantClaim = "tenant_id";
}
