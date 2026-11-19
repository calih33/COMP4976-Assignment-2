namespace MEMS.Data.Tenancy;

/// <summary>
/// Scoped, mutable implementation of <see cref="ITenantContext"/>. Starts with no access (fail closed);
/// the host calls one of the Set methods once it knows who the caller is.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public int? MunicipalityId { get; private set; }

    public bool IsSuperAdmin { get; private set; }

    public bool CanWrite { get; private set; }

    /// <summary>Anonymous visitor of one tenant's public site (read-only).</summary>
    public void SetPublic(int municipalityId)
    {
        MunicipalityId = municipalityId;
        IsSuperAdmin = false;
        CanWrite = false;
    }

    /// <summary>
    /// Signed-in admin. A Municipality Admin passes their own tenant. A Super Admin passes null to see all
    /// tenants, or a tenant id to work inside one tenant (e.g. <c>/api/surrey/...</c>).
    /// </summary>
    public void SetAdmin(int? municipalityId, bool isSuperAdmin)
    {
        MunicipalityId = municipalityId;
        IsSuperAdmin = isSuperAdmin;
        CanWrite = true;
    }

    /// <summary>Migrations and seeding: full access to every tenant.</summary>
    public void SetSystem()
    {
        MunicipalityId = null;
        IsSuperAdmin = true;
        CanWrite = true;
    }
}
