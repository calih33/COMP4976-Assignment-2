namespace MEMS.Data.Tenancy;

/// <summary>
/// Who is asking, and for which tenant. Drives the EF Core global query filters and every service check.
/// Filled in per request by the host: the API from the route slug + JWT, Admin from the signed-in user's claims.
/// </summary>
public interface ITenantContext
{
    /// <summary>The active tenant. Null means "no tenant" (sees nothing) unless <see cref="IsSuperAdmin"/>, then "all tenants".</summary>
    int? MunicipalityId { get; }

    /// <summary>Super Admin (or the system during seeding): may manage every tenant.</summary>
    bool IsSuperAdmin { get; }

    /// <summary>A signed-in admin (or the system). Anonymous public visitors can only read.</summary>
    bool CanWrite { get; }

    /// <summary>True when the caller may change data belonging to <paramref name="municipalityId"/>.</summary>
    bool CanManage(int municipalityId) =>
        CanWrite && (IsSuperAdmin || MunicipalityId == municipalityId);
}
