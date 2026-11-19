using Microsoft.EntityFrameworkCore;

namespace MEMS.Data.Services;

public sealed record DashboardCounts(int Municipalities, int Positions, int Candidates);

public interface IDashboardService
{
    /// <summary>Totals for the caller's scope: everything for a Super Admin, their own tenant for a Municipality Admin.</summary>
    Task<DashboardCounts> GetCountsAsync(CancellationToken ct = default);
}

public sealed class DashboardService(AppDbContextFactory factory) : IDashboardService
{
    public async Task<DashboardCounts> GetCountsAsync(CancellationToken ct = default)
    {
        await using var db = factory.Create();
        return new DashboardCounts(
            await db.Municipalities.CountAsync(ct),
            await db.Positions.CountAsync(ct),
            await db.Candidates.CountAsync(ct));
    }
}
