using MEMS.Data.Tenancy;
using MEMS.Images;
using MEMS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MEMS.Data.Services;

public interface ICandidateService
{
    /// <summary>Candidates in the active tenant(s), optionally filtered by name (case-insensitive) and position.</summary>
    Task<IReadOnlyList<Candidate>> ListAsync(string? search = null, int? positionId = null, CancellationToken ct = default);

    Task<Candidate?> GetAsync(int id, CancellationToken ct = default);

    Task<ServiceResult<Candidate>> CreateAsync(Candidate input, ImageUpload? photo, CancellationToken ct = default);

    Task<ServiceResult<Candidate>> UpdateAsync(int id, Candidate input, ImageUpload? photo, bool removePhoto = false, CancellationToken ct = default);

    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class CandidateService(AppDbContextFactory factory, ITenantContext tenant, ILogger<CandidateService> logger)
    : ICandidateService
{
    public async Task<IReadOnlyList<Candidate>> ListAsync(string? search = null, int? positionId = null, CancellationToken ct = default)
    {
        await using var db = factory.Create();
        IQueryable<Candidate> query = db.Candidates
            .AsNoTracking()
            .Include(c => c.Position)
            .Include(c => c.Municipality);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // SQL Server's default collation is case-insensitive, so "chen" finds "Chen" (A1 needed ToLower for SQLite).
            var term = search.Trim();
            query = query.Where(c => c.Name.Contains(term));
        }

        if (positionId is not null)
        {
            query = query.Where(c => c.PositionId == positionId);
        }

        return await query
            .OrderBy(c => c.Municipality!.Name)
            .ThenBy(c => c.Position!.Name)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<Candidate?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = factory.Create();
        return await db.Candidates
            .AsNoTracking()
            .Include(c => c.Position)
            .Include(c => c.Municipality)
            .FirstOrDefaultAsync(c => c.CandidateId == id, ct);
    }

    public async Task<ServiceResult<Candidate>> CreateAsync(Candidate input, ImageUpload? photo, CancellationToken ct = default)
    {
        if (!tenant.CanWrite)
        {
            return ServiceResult<Candidate>.Forbidden();
        }

        await using var db = factory.Create();

        // Query filter: a position from another tenant is invisible, so it reads as "not a valid choice".
        var position = await db.Positions.FirstOrDefaultAsync(p => p.PositionId == input.PositionId, ct);
        if (position is not null && !tenant.CanManage(position.MunicipalityId))
        {
            return ServiceResult<Candidate>.Forbidden();
        }

        Normalize(input);
        input.MunicipalityId = position?.MunicipalityId ?? 0;

        var errors = Validate(input, photo, position);
        if (errors.Count > 0)
        {
            return ServiceResult<Candidate>.Invalid(errors);
        }

        var candidate = new Candidate { DateCreated = DateTime.UtcNow };
        CopyFields(input, candidate);
        if (photo is not null)
        {
            candidate.PhotoImage = EntityValidation.ToStoredImage(photo);
        }

        db.Candidates.Add(candidate);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created candidate {CandidateId} for position {PositionId} in municipality {MunicipalityId}",
            candidate.CandidateId, candidate.PositionId, candidate.MunicipalityId);
        return ServiceResult<Candidate>.Ok(candidate);
    }

    public async Task<ServiceResult<Candidate>> UpdateAsync(int id, Candidate input, ImageUpload? photo, bool removePhoto = false, CancellationToken ct = default)
    {
        await using var db = factory.Create();
        var existing = await db.Candidates.Include(c => c.PhotoImage).FirstOrDefaultAsync(c => c.CandidateId == id, ct);
        if (existing is null)
        {
            return ServiceResult<Candidate>.NotFound();
        }

        // Check the stored record's tenant, then the newly chosen position's tenant (A1's double check).
        if (!tenant.CanManage(existing.MunicipalityId))
        {
            return ServiceResult<Candidate>.Forbidden();
        }

        var position = await db.Positions.FirstOrDefaultAsync(p => p.PositionId == input.PositionId, ct);
        if (position is not null && !tenant.CanManage(position.MunicipalityId))
        {
            return ServiceResult<Candidate>.Forbidden();
        }

        Normalize(input);
        input.MunicipalityId = position?.MunicipalityId ?? 0;

        var errors = Validate(input, photo, position);
        if (errors.Count > 0)
        {
            return ServiceResult<Candidate>.Invalid(errors);
        }

        CopyFields(input, existing);

        if (photo is not null || removePhoto)
        {
            if (existing.PhotoImage is not null)
            {
                db.StoredImages.Remove(existing.PhotoImage);
            }

            existing.PhotoImage = photo is null ? null : EntityValidation.ToStoredImage(photo);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Updated candidate {CandidateId}", id);
        return ServiceResult<Candidate>.Ok(existing);
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = factory.Create();
        var candidate = await db.Candidates.Include(c => c.PhotoImage).FirstOrDefaultAsync(c => c.CandidateId == id, ct);
        if (candidate is null)
        {
            return ServiceResult.NotFound();
        }

        if (!tenant.CanManage(candidate.MunicipalityId))
        {
            return ServiceResult.Forbidden();
        }

        db.Candidates.Remove(candidate);
        if (candidate.PhotoImage is not null)
        {
            db.StoredImages.Remove(candidate.PhotoImage);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Deleted candidate {CandidateId}", id);
        return ServiceResult.Ok();
    }

    private static void Normalize(Candidate input)
    {
        input.Name = input.Name.Trim();
        input.Profile = input.Profile.Trim();
        input.Website = EntityValidation.Clean(input.Website);
        input.Email = EntityValidation.Clean(input.Email);
    }

    private static void CopyFields(Candidate from, Candidate to)
    {
        to.Name = from.Name;
        to.Profile = from.Profile;
        to.Website = from.Website;
        to.Email = from.Email;
        to.PositionId = from.PositionId;
        to.MunicipalityId = from.MunicipalityId;
    }

    private static Dictionary<string, string[]> Validate(Candidate input, ImageUpload? photo, Position? position)
    {
        var errors = EntityValidation.Validate(input);

        if (position is null)
        {
            errors[nameof(Candidate.PositionId)] = ["Choose a position in your municipality."];
        }

        if (photo is not null && ImageRules.Validate(photo) is { } photoError)
        {
            errors["Photo"] = [photoError];
        }

        return errors;
    }
}
