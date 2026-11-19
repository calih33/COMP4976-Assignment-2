using MEMS.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data.Services;

/// <summary>
/// Creates a short-lived <see cref="AppDbContext"/> bound to the current tenant. Services open one per operation,
/// which is the recommended pattern for Blazor Server (a circuit-wide context breaks on overlapping async calls).
/// </summary>
public sealed class AppDbContextFactory(DbContextOptions<AppDbContext> options, ITenantContext tenant)
{
    public AppDbContext Create() => new(options, tenant);
}
