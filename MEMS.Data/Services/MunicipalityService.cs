using MEMS.Data.Tenancy;
using MEMS.Images;
using MEMS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MEMS.Data.Services;

public interface IMunicipalityService
{
    /// <summary>Municipalities the caller can see (one for a Municipality Admin, all for a Super Admin).</summary>
    Task<IReadOnlyList<Municipality>> ListAsync(CancellationToken ct = default);

    /// <summary>One municipality with positions and candidates, or null if missing or another tenant's.</summary>
    Task<Municipality?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Tenant resolution from a URL slug (case-insensitive). Ignores tenant filters; read-only.</summary>
    Task<Municipality?> FindBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Every tenant, for the public municipality picker. Read-only, no positions/candidates.</summary>
    Task<IReadOnlyList<Municipality>> ListAllTenantsAsync(CancellationToken ct = default);

    Task<ServiceResult<Municipality>> CreateAsync(Municipality input, ImageUpload? logo, CancellationToken ct = default);

    Task<ServiceResult<Municipality>> UpdateAsync(int id, Municipality input, ImageUpload? logo, bool removeLogo = false, CancellationToken ct = default);

    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class MunicipalityService(AppDbContextFactory factory, ITenantContext tenant, ILogger<MunicipalityService> logger)
    : IMunicipalityService
{
    public async Task<IReadOnlyList<Municipality>> ListAsync(CancellationToken ct = default)
    {
        await using var db = factory.Create();
        return await db.Municipalities
            .AsNoTracking()
            .Include(m => m.Positions)
            .OrderBy(m => m.Name)
            .ToListAsync(ct);
    }

    public async Task<Municipality?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = factory.Create();
        return await db.Municipalities
            .AsNoTracking()
            .Include(m => m.Positions.OrderBy(p => p.Name))
                .ThenInclude(p => p.Candidates.OrderBy(c => c.Name))
            .FirstOrDefaultAsync(m => m.MunicipalityId == id, ct);
    }

    public async Task<Municipality?> FindBySlugAsync(string slug, CancellationToken ct = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        await using var db = factory.Create();
        return await db.Municipalities
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Slug == normalized, ct);
    }

    public async Task<IReadOnlyList<Municipality>> ListAllTenantsAsync(CancellationToken ct = default)
    {
        await using var db = factory.Create();
        return await db.Municipalities
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .ToListAsync(ct);
    }

    public async Task<ServiceResult<Municipality>> CreateAsync(Municipality input, ImageUpload? logo, CancellationToken ct = default)
    {
        if (!tenant.CanWrite || !tenant.IsSuperAdmin)
        {
            return ServiceResult<Municipality>.Forbidden();
        }

        Normalize(input);
        await using var db = factory.Create();

        var errors = await ValidateAsync(db, input, logo, existingId: null, ct);
        if (errors.Count > 0)
        {
            return ServiceResult<Municipality>.Invalid(errors);
        }

        var municipality = new Municipality();
        CopyFields(input, municipality, includeSlug: true);
        if (logo is not null)
        {
            municipality.LogoImage = EntityValidation.ToStoredImage(logo);
        }

        db.Municipalities.Add(municipality);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created municipality {MunicipalityId} ({Slug})", municipality.MunicipalityId, municipality.Slug);
        return ServiceResult<Municipality>.Ok(municipality);
    }

    public async Task<ServiceResult<Municipality>> UpdateAsync(int id, Municipality input, ImageUpload? logo, bool removeLogo = false, CancellationToken ct = default)
    {
        await using var db = factory.Create();

        // Query filter: another tenant's municipality is simply not found.
        var existing = await db.Municipalities.Include(m => m.LogoImage).FirstOrDefaultAsync(m => m.MunicipalityId == id, ct);
        if (existing is null)
        {
            return ServiceResult<Municipality>.NotFound();
        }

        if (!tenant.CanManage(existing.MunicipalityId))
        {
            return ServiceResult<Municipality>.Forbidden();
        }

        Normalize(input);
        if (!tenant.IsSuperAdmin && input.Slug != existing.Slug)
        {
            return ServiceResult<Municipality>.Invalid(nameof(Municipality.Slug), "Only a Super Admin can change the URL slug.");
        }

        var errors = await ValidateAsync(db, input, logo, existingId: id, ct);
        if (errors.Count > 0)
        {
            return ServiceResult<Municipality>.Invalid(errors);
        }

        CopyFields(input, existing, includeSlug: tenant.IsSuperAdmin);

        if (logo is not null || removeLogo)
        {
            if (existing.LogoImage is not null)
            {
                db.StoredImages.Remove(existing.LogoImage);
            }

            existing.LogoImage = logo is null ? null : EntityValidation.ToStoredImage(logo);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Updated municipality {MunicipalityId}", id);
        return ServiceResult<Municipality>.Ok(existing);
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (!tenant.CanWrite || !tenant.IsSuperAdmin)
        {
            return ServiceResult.Forbidden();
        }

        await using var db = factory.Create();
        var municipality = await db.Municipalities
            .Include(m => m.LogoImage)
            .Include(m => m.Positions).ThenInclude(p => p.Candidates).ThenInclude(c => c.PhotoImage)
            .FirstOrDefaultAsync(m => m.MunicipalityId == id, ct);

        if (municipality is null)
        {
            return ServiceResult.NotFound();
        }

        // Images aren't cascade-deleted (they're shared FK targets), so remove them explicitly.
        var candidates = municipality.Positions.SelectMany(p => p.Candidates).ToList();
        db.StoredImages.RemoveRange(candidates.Where(c => c.PhotoImage is not null).Select(c => c.PhotoImage!));
        if (municipality.LogoImage is not null)
        {
            db.StoredImages.Remove(municipality.LogoImage);
        }

        // The tenant's admin accounts can't outlive the tenant.
        var admins = await db.Users.Where(u => u.MunicipalityId == id).ToListAsync(ct);
        db.Users.RemoveRange(admins);

        db.Candidates.RemoveRange(candidates);
        db.Positions.RemoveRange(municipality.Positions);
        db.Municipalities.Remove(municipality);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Deleted municipality {MunicipalityId} with {PositionCount} positions, {CandidateCount} candidates and {AdminCount} admin accounts",
            id, municipality.Positions.Count, candidates.Count, admins.Count);
        return ServiceResult.Ok();
    }

    private static void Normalize(Municipality input)
    {
        input.Name = input.Name.Trim();
        input.Slug = input.Slug.Trim().ToLowerInvariant();
        input.Province = input.Province.Trim();
        input.Description = EntityValidation.Clean(input.Description);
        input.ThemeColor = input.ThemeColor.Trim().ToLowerInvariant();
    }

    private static void CopyFields(Municipality from, Municipality to, bool includeSlug)
    {
        to.Name = from.Name;
        to.Province = from.Province;
        to.ElectionDate = from.ElectionDate;
        to.Description = from.Description;
        to.ThemeColor = from.ThemeColor;
        if (includeSlug)
        {
            to.Slug = from.Slug;
        }
    }

    private static async Task<Dictionary<string, string[]>> ValidateAsync(
        AppDbContext db, Municipality input, ImageUpload? logo, int? existingId, CancellationToken ct)
    {
        var errors = EntityValidation.Validate(input);

        // Uniqueness is global, so look across every tenant.
        var all = db.Municipalities.IgnoreQueryFilters().Where(m => m.MunicipalityId != existingId);
        if (await all.AnyAsync(m => m.Name == input.Name, ct))
        {
            errors[nameof(Municipality.Name)] = ["A municipality with this name already exists."];
        }

        if (await all.AnyAsync(m => m.Slug == input.Slug, ct))
        {
            errors[nameof(Municipality.Slug)] = ["This URL slug is already used by another municipality."];
        }

        if (logo is not null && ImageRules.Validate(logo) is { } logoError)
        {
            errors["Logo"] = [logoError];
        }

        return errors;
    }
}
