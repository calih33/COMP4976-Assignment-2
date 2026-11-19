using MEMS.Data.Tenancy;
using MEMS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MEMS.Data.Services;

public interface IPositionService
{
    Task<IReadOnlyList<Position>> ListAsync(CancellationToken ct = default);

    Task<Position?> GetAsync(int id, CancellationToken ct = default);

    Task<ServiceResult<Position>> CreateAsync(Position input, CancellationToken ct = default);

    /// <summary>Updates name and seats. A position can't move to another tenant.</summary>
    Task<ServiceResult<Position>> UpdateAsync(int id, Position input, CancellationToken ct = default);

    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class PositionService(AppDbContextFactory factory, ITenantContext tenant, ILogger<PositionService> logger)
    : IPositionService
{
    public async Task<IReadOnlyList<Position>> ListAsync(CancellationToken ct = default)
    {
        await using var db = factory.Create();
        return await db.Positions
            .AsNoTracking()
            .Include(p => p.Municipality)
            .Include(p => p.Candidates)
            .OrderBy(p => p.Municipality!.Name)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<Position?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = factory.Create();
        return await db.Positions
            .AsNoTracking()
            .Include(p => p.Municipality)
            .Include(p => p.Candidates.OrderBy(c => c.Name))
            .FirstOrDefaultAsync(p => p.PositionId == id, ct);
    }

    public async Task<ServiceResult<Position>> CreateAsync(Position input, CancellationToken ct = default)
    {
        if (!tenant.CanWrite)
        {
            return ServiceResult<Position>.Forbidden();
        }

        // Municipality Admins (and a Super Admin working inside one tenant) always create in the active tenant;
        // the posted MunicipalityId only matters for a Super Admin working across all tenants.
        input.MunicipalityId = tenant.MunicipalityId ?? input.MunicipalityId;
        input.Name = input.Name.Trim();

        if (!tenant.CanManage(input.MunicipalityId))
        {
            return ServiceResult<Position>.Forbidden();
        }

        await using var db = factory.Create();
        var errors = EntityValidation.Validate(input);

        if (!await db.Municipalities.AnyAsync(m => m.MunicipalityId == input.MunicipalityId, ct))
        {
            errors[nameof(Position.MunicipalityId)] = ["Choose a municipality."];
        }
        else if (await NameTakenAsync(db, input.MunicipalityId, input.Name, null, ct))
        {
            errors[nameof(Position.Name)] = ["This municipality already has a position with this name."];
        }

        if (errors.Count > 0)
        {
            return ServiceResult<Position>.Invalid(errors);
        }

        var position = new Position { Name = input.Name, SeatsAvailable = input.SeatsAvailable, MunicipalityId = input.MunicipalityId };
        db.Positions.Add(position);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created position {PositionId} in municipality {MunicipalityId}", position.PositionId, position.MunicipalityId);
        return ServiceResult<Position>.Ok(position);
    }

    public async Task<ServiceResult<Position>> UpdateAsync(int id, Position input, CancellationToken ct = default)
    {
        await using var db = factory.Create();
        var existing = await db.Positions.FirstOrDefaultAsync(p => p.PositionId == id, ct);
        if (existing is null)
        {
            return ServiceResult<Position>.NotFound();
        }

        if (!tenant.CanManage(existing.MunicipalityId))
        {
            return ServiceResult<Position>.Forbidden();
        }

        input.Name = input.Name.Trim();
        input.MunicipalityId = existing.MunicipalityId;

        var errors = EntityValidation.Validate(input);
        if (await NameTakenAsync(db, existing.MunicipalityId, input.Name, id, ct))
        {
            errors[nameof(Position.Name)] = ["This municipality already has a position with this name."];
        }

        if (errors.Count > 0)
        {
            return ServiceResult<Position>.Invalid(errors);
        }

        existing.Name = input.Name;
        existing.SeatsAvailable = input.SeatsAvailable;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Updated position {PositionId}", id);
        return ServiceResult<Position>.Ok(existing);
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = factory.Create();
        var position = await db.Positions
            .Include(p => p.Candidates).ThenInclude(c => c.PhotoImage)
            .FirstOrDefaultAsync(p => p.PositionId == id, ct);

        if (position is null)
        {
            return ServiceResult.NotFound();
        }

        if (!tenant.CanManage(position.MunicipalityId))
        {
            return ServiceResult.Forbidden();
        }

        db.StoredImages.RemoveRange(position.Candidates.Where(c => c.PhotoImage is not null).Select(c => c.PhotoImage!));
        db.Candidates.RemoveRange(position.Candidates);
        db.Positions.Remove(position);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Deleted position {PositionId} and {CandidateCount} candidates", id, position.Candidates.Count);
        return ServiceResult.Ok();
    }

    private static Task<bool> NameTakenAsync(AppDbContext db, int municipalityId, string name, int? excludeId, CancellationToken ct) =>
        db.Positions.IgnoreQueryFilters()
            .AnyAsync(p => p.MunicipalityId == municipalityId && p.Name == name && p.PositionId != excludeId, ct);
}
