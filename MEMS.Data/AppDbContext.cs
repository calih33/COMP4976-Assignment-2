using MEMS.Data.Tenancy;
using MEMS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MEMS.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<StoredImage> StoredImages => Set<StoredImage>();

    // Read by the global query filters. EF Core re-evaluates these per query, per context instance.
    private int? CurrentMunicipalityId => tenant.MunicipalityId;
    private bool AllTenants => tenant.IsSuperAdmin && tenant.MunicipalityId == null;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasOne(u => u.Municipality)
                .WithMany()
                .HasForeignKey(u => u.MunicipalityId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StoredImage>(entity =>
        {
            entity.Property(i => i.ContentType).HasMaxLength(50).IsRequired();
            entity.Property(i => i.Data).IsRequired();
        });

        builder.Entity<Municipality>(entity =>
        {
            entity.Property(m => m.Name).HasMaxLength(100).IsRequired();
            entity.Property(m => m.Slug).HasMaxLength(50).IsRequired();
            entity.Property(m => m.Province).HasMaxLength(50).IsRequired();
            entity.Property(m => m.Description).HasMaxLength(1000);
            entity.Property(m => m.ThemeColor).HasMaxLength(7).IsFixedLength().IsRequired();

            entity.HasIndex(m => m.Name).IsUnique();
            entity.HasIndex(m => m.Slug).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint("CK_Municipalities_Slug", "[Slug] COLLATE Latin1_General_BIN2 NOT LIKE '%[^a-z0-9-]%'"));

            entity.HasOne(m => m.LogoImage)
                .WithMany()
                .HasForeignKey(m => m.LogoImageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(m => AllTenants || m.MunicipalityId == CurrentMunicipalityId);
        });

        builder.Entity<Position>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(50).IsRequired();
            entity.ToTable(t => t.HasCheckConstraint("CK_Positions_SeatsAvailable", "[SeatsAvailable] BETWEEN 1 AND 50"));

            entity.HasOne(p => p.Municipality)
                .WithMany(m => m.Positions)
                .HasForeignKey(p => p.MunicipalityId)
                .OnDelete(DeleteBehavior.Cascade);

            // A municipality can't have two positions with the same name.
            entity.HasIndex(p => new { p.MunicipalityId, p.Name }).IsUnique();

            // Lets Candidate reference (PositionId, MunicipalityId) so the database guarantees a candidate's
            // tenant matches its position's tenant.
            entity.HasAlternateKey(p => new { p.PositionId, p.MunicipalityId });

            entity.HasQueryFilter(p => AllTenants || p.MunicipalityId == CurrentMunicipalityId);
        });

        builder.Entity<Candidate>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Profile).HasMaxLength(Candidate.ProfileMaxLength).IsRequired();
            entity.Property(c => c.Website).HasMaxLength(200);
            entity.Property(c => c.Email).HasMaxLength(100);

            entity.HasOne(c => c.Position)
                .WithMany(p => p.Candidates)
                .HasForeignKey(c => new { c.PositionId, c.MunicipalityId })
                .HasPrincipalKey(p => new { p.PositionId, p.MunicipalityId })
                .OnDelete(DeleteBehavior.Cascade);

            // NoAction: SQL Server rejects two cascade paths (Municipality -> Position -> Candidate and
            // Municipality -> Candidate). Candidates are removed through their position's cascade.
            entity.HasOne(c => c.Municipality)
                .WithMany(m => m.Candidates)
                .HasForeignKey(c => c.MunicipalityId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(c => c.PhotoImage)
                .WithMany()
                .HasForeignKey(c => c.PhotoImageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(c => new { c.MunicipalityId, c.Name });

            entity.HasQueryFilter(c => AllTenants || c.MunicipalityId == CurrentMunicipalityId);
        });
    }
}
